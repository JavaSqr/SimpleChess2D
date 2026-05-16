using UnityEngine;

public class AnimatorSupport : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    private void Start()
    {
        if (audioSource != null) return;
        if (!TryGetComponent(out audioSource))
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void PlayOneShotSound(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
}