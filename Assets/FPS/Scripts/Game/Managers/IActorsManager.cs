using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.Game
{
    public interface IActorsManager
    {
        List<Actor> Actors { get; }
        GameObject Player { get; }
        void SetPlayer(GameObject player);
    }
}
