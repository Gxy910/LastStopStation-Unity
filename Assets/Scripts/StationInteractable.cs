using UnityEngine;

namespace YuDian
{
    public sealed class StationInteractable : MonoBehaviour
    {
        [SerializeField] private StationTarget target;
        [SerializeField] private string displayName;

        public StationTarget Target => target;
        public string DisplayName => displayName;

        public void Configure(StationTarget stationTarget, string label)
        {
            target = stationTarget;
            displayName = label;
        }
    }
}
