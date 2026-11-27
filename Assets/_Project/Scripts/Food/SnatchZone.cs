using System;
using UnityEngine;

namespace DragonRecipe.Food
{
    /// <summary>드래곤 발톱 아래 트리거. 먹이가 들어오면 낚아챈다.</summary>
    [RequireComponent(typeof(Collider))]
    public class SnatchZone : MonoBehaviour
    {
        public event Action<Prey> Snatched;
        public bool Armed { get; set; }

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!Armed) return;
            var prey = other.GetComponentInParent<Prey>();
            if (prey != null && prey.IsAvailable)
                Snatched?.Invoke(prey);
        }
    }
}
