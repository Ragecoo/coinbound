using UnityEngine;

namespace Coinbound
{
    /// <summary>Lives on the UI root so leaving the menu can fade its sound out.</summary>
    public sealed class DungeonMenuAmbience : MonoBehaviour
    {
        private GameObject menu;
        private AudioSource source;

        public void Initialize(GameObject menuPage)
        {
            menu = menuPage;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.ignoreListenerPause = true;
            source.volume = 0;
            source.clip = Resources.Load<AudioClip>("Audio/menu-ambience");
        }

        private void Update()
        {
            if (source == null || source.clip == null) return;
            bool audible = menu != null && menu.activeInHierarchy;
            if (audible && !source.isPlaying) source.Play();
            source.volume = Mathf.MoveTowards(source.volume, audible ? .55f : 0,
                Time.unscaledDeltaTime * (audible ? .22f : .8f));
            if (!audible && source.volume == 0 && source.isPlaying) source.Pause();
        }

        private void OnDisable()
        {
            if (source == null) return;
            source.Stop();
            source.volume = 0;
        }
    }
}
