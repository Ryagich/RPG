using TMPro;
using UnityEngine;

namespace UI.Compass
{
    public sealed class CompassController : MonoBehaviour
    {
        private Transform _cameraTransform;
        private TMP_Text _directionLabel;
        private CompassConfig _config;

        public void Initialize(Transform cameraTransform, TMP_Text directionLabel, CompassConfig config)
        {
            _cameraTransform = cameraTransform;
            _directionLabel = directionLabel;
            _config = config;
        }

        private void Update()
        {

            float heading = Mathf.Repeat(_cameraTransform.eulerAngles.y - _config.NorthOffset, 360f);

            float sectorStep = 360f / _config.Labels.Length;

            int index = Mathf.RoundToInt(heading / sectorStep) % _config.Labels.Length;

            _directionLabel.SetText(_config.Labels[index]);
        }
    }
}
