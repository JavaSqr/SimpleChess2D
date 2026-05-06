using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class AnimatorSupport : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    private void Start()
    {
        if (audioSource == null)
        {
            if (TryGetComponent<AudioSource>(out AudioSource au))
            {
                audioSource = au;
            }
            else
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    public void PlayOneShotSound(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
}
