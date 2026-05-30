using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;

namespace Unity.FPS.Game
{
    // ============================================================================
    // MeshCombineUtility — статическая утилита, делающая саму работу по склейке.
    // Используется MeshCombiner'ом.
    //
    // Идея:
    //  1) Группируем рендереры по уникальной комбинации (Material + SubmeshIndex +
    //     shadow-настройки) — потому что в один меш можно положить только меши
    //     с ОДНИМ материалом.
    //  2) Для каждой группы строим единый меш через Mesh.CombineMeshes.
    //  3) Опционально уничтожаем оригиналы (чтобы не рисовать их дважды).
    // ============================================================================
    public static class MeshCombineUtility
    {
        // Данные одной «партии» — все меши, которые объединятся в один.
        public class RenderBatchData
        {
            // Меш + его мировая матрица трансформации.
            // Без трансформации все меши слепятся в точке (0,0,0), без поворота и масштаба.
            public class MeshAndTrs
            {
                public Mesh Mesh;
                public Matrix4x4 Trs;

                public MeshAndTrs(Mesh m, Matrix4x4 t)
                {
                    Mesh = m;
                    Trs = t;
                }
            }

            // Материал — ключ группировки.
            public Material Material;
            // Индекс subMesh внутри исходного меша. Многоматериальные меши
            // делятся на subMesh'и, каждый со своим материалом.
            public int SubmeshIndex = 0;
            // Настройки теней должны совпадать у объединяемых мешей, иначе
            // визуально получим расхождение с оригиналом.
            public ShadowCastingMode ShadowMode;
            public bool ReceiveShadows;
            public MotionVectorGenerationMode MotionVectors;
            public List<MeshAndTrs> MeshesWithTrs = new List<MeshAndTrs>();
        }

        // Что делать с оригинальными объектами после склейки.
        public enum RendererDisposeMethod
        {
            DestroyGameObject,         // Уничтожить весь GameObject.
            DestroyRendererAndFilter,  // Снять только рендеринг (коллайдеры останутся).
            DisableGameObject,         // Деактивировать (без удаления).
            DisableRenderer,           // Выключить только рендерер.
        }

        public static void Combine(List<MeshRenderer> renderers, RendererDisposeMethod disposeMethod,
            string newObjectName)
        {
            int renderersCount = renderers.Count;

            List<RenderBatchData> renderBatches = new List<RenderBatchData>();

            // Build render batches for all unique material + submeshIndex combinations
            // ----- 1) Раскидываем меши по группам -----
            for (int i = 0; i < renderersCount; i++)
            {
                MeshRenderer meshRenderer = renderers[i];

                if (meshRenderer == null)
                    continue;

                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();

                if (meshFilter == null)
                    continue;

                Mesh mesh = meshFilter.sharedMesh;

                if (mesh == null)
                    continue;

                Transform t = meshRenderer.GetComponent<Transform>();
                Material[] materials = meshRenderer.sharedMaterials;

                // Цикл по сабмешам — каждый сабмеш отдельно идёт в свою партию.
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (materials[s] == null)
                        continue;

                    int batchIndex = GetExistingRenderBatch(renderBatches, materials[s], meshRenderer, s);
                    if (batchIndex >= 0)
                    {
                        // Группа уже есть — добавляем меш с его мировой матрицей.
                        // Matrix4x4.TRS = Translation * Rotation * Scale.
                        // lossyScale — мировой масштаб с учётом всех родителей.
                        renderBatches[batchIndex].MeshesWithTrs
                            .Add(new RenderBatchData.MeshAndTrs(mesh,
                                Matrix4x4.TRS(t.position, t.rotation, t.lossyScale)));
                    }
                    else
                    {
                        // Группы нет — создаём.
                        RenderBatchData newBatchData = new RenderBatchData();
                        newBatchData.Material = materials[s];
                        newBatchData.SubmeshIndex = s;
                        newBatchData.ShadowMode = meshRenderer.shadowCastingMode;
                        newBatchData.ReceiveShadows = meshRenderer.receiveShadows;
                        newBatchData.MeshesWithTrs.Add(new RenderBatchData.MeshAndTrs(mesh,
                            Matrix4x4.TRS(t.position, t.rotation, t.lossyScale)));

                        renderBatches.Add(newBatchData);
                    }
                }

                // Destroy probuilder component if present
                // ProBuilder — это инструмент для создания геометрии прямо в редакторе.
                // После склейки эти данные не нужны (меш стал «обычным»), убираем.
                ProBuilderMesh pbm = meshRenderer.GetComponent<ProBuilderMesh>();
                if (pbm)
                {
                    GameObject.Destroy(pbm);
                }

                // Способ избавиться от оригинала. В рантайме нельзя DestroyImmediate
                // (Unity сломается), а в редакторе наоборот — обычный Destroy не сработает.
                switch (disposeMethod)
                {
                    case RendererDisposeMethod.DestroyGameObject:
                        if (Application.isPlaying)
                        {
                            GameObject.Destroy(meshRenderer.gameObject);
                        }
                        else
                        {
                            GameObject.DestroyImmediate(meshRenderer.gameObject);
                        }

                        break;
                    case RendererDisposeMethod.DestroyRendererAndFilter:
                        if (Application.isPlaying)
                        {
                            GameObject.Destroy(meshRenderer);
                            GameObject.Destroy(meshFilter);
                        }
                        else
                        {
                            GameObject.DestroyImmediate(meshRenderer);
                            GameObject.DestroyImmediate(meshFilter);
                        }

                        break;
                    case RendererDisposeMethod.DisableGameObject:
                        meshRenderer.gameObject.SetActive(false);
                        break;
                    case RendererDisposeMethod.DisableRenderer:
                        meshRenderer.enabled = false;
                        break;
                }
            }

            // Combine each unique render batch
            // ----- 2) Каждую группу собираем в один меш -----
            for (int i = 0; i < renderBatches.Count; i++)
            {
                RenderBatchData rbd = renderBatches[i];

                Mesh newMesh = new Mesh();
                // UInt32 индексы — позволяют больше 65к вершин. Для больших уровней нужно.
                newMesh.indexFormat = IndexFormat.UInt32;
                // CombineInstance — структура, которую ждёт Mesh.CombineMeshes.
                CombineInstance[] combineInstances = new CombineInstance[rbd.MeshesWithTrs.Count];

                for (int j = 0; j < rbd.MeshesWithTrs.Count; j++)
                {
                    combineInstances[j].subMeshIndex = rbd.SubmeshIndex;
                    combineInstances[j].mesh = rbd.MeshesWithTrs[j].Mesh;
                    combineInstances[j].transform = rbd.MeshesWithTrs[j].Trs;
                }

                // Create mesh
                newMesh.CombineMeshes(combineInstances);
                // Bounds нужны для frustum culling — без пересчёта могут оказаться нулевыми.
                newMesh.RecalculateBounds();

                // Create the gameObject
                // Создаём новый объект с новым мешем и материалом.
                GameObject combinedObject = new GameObject(newObjectName);
                MeshFilter mf = combinedObject.AddComponent<MeshFilter>();
                mf.sharedMesh = newMesh;
                MeshRenderer mr = combinedObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = rbd.Material;
                mr.shadowCastingMode = rbd.ShadowMode;
            }
        }

        // Линейный поиск партии под текущий рендерер. List, а не Dictionary,
        // потому что ключ — это комбинация 4 полей, и групп обычно немного.
        static int GetExistingRenderBatch(List<RenderBatchData> renderBatches, Material mat, MeshRenderer ren, int submeshIndex)
        {
            for (int i = 0; i < renderBatches.Count; i++)
            {
                RenderBatchData data = renderBatches[i];
                if (data.Material == mat &&
                    data.SubmeshIndex == submeshIndex &&
                    data.ShadowMode == ren.shadowCastingMode &&
                    data.ReceiveShadows == ren.receiveShadows)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
