using UnityEngine;

namespace Unity.FPS.Game
{
    // ============================================================================
    // PooledParticleAutoRelease — автоматически возвращает объект с ParticleSystem
    // обратно в пул, когда частицы закончили проигрываться.
    //
    // Зачем: эффекты (искры, дым, взрывы) — это короткие частицы. Без этого
    // компонента нужно было бы вручную писать корутины и подсчитывать длительность,
    // что хрупко и легко забыть.
    //
    // Как работает: на OnEnable (когда пул выдал объект) запускаем частицы,
    // на каждом Update проверяем IsAlive — когда вернёт false (все частицы
    // умерли), отдаём объект в пул.
    //
    // GameObjectPoolManager сам добавляет этот компонент, если на префабе есть
    // ParticleSystem — см. CLAUDE.md «Object Pooling».
    // ============================================================================
    public class PooledParticleAutoRelease : MonoBehaviour
    {
        // Кешированная система частиц — чтобы не искать каждый кадр.
        ParticleSystem m_ParticleSystem;
        // Флаг «сейчас играет». Нужен, потому что IsAlive(true) может вернуть
        // true, ещё не успев запуститься; и потому что не хотим тратить
        // Update-проверку, когда играть нечего.
        bool m_IsPlaying;

        void Awake()
        {
            // GetComponentInChildren — потому что ParticleSystem часто лежит
            // не на корневом объекте, а на дочернем (например, при сложных VFX
            // из нескольких систем).
            m_ParticleSystem = GetComponentInChildren<ParticleSystem>();
        }

        // OnEnable вызывается каждый раз, когда объект активируют. Для пула это
        // момент «выдан игре». Здесь подходит перезапустить эффект.
        void OnEnable()
        {
            m_IsPlaying = false;
            if (m_ParticleSystem != null)
            {
                // Play(true) — true означает «включая всех потомков».
                // Без true сложный VFX из нескольких систем сыграет частично.
                m_ParticleSystem.Play(true);
                m_IsPlaying = true;
            }
        }

        void Update()
        {
            // IsAlive(true) — есть ли ещё живые частицы у этой системы и потомков.
            // Когда вернёт false — анимация эффекта закончилась.
            if (m_IsPlaying && !m_ParticleSystem.IsAlive(true))
            {
                // Сбрасываем флаг до того, как объект снова дёрнут из пула,
                // чтобы не повторить Release дважды.
                m_IsPlaying = false;
                // Release деактивирует объект и кладёт его обратно в пул.
                // Это НЕ Destroy — память не освобождается, GC отдыхает.
                GameObjectPoolManager.Instance.Release(gameObject);
            }
        }
    }
}
