using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ButtonSoundPlayer : MonoBehaviour
{
    [SerializeField] private UnityEvent _awake;
    [SerializeField] private AudioSource _source;
    [SerializeField] private AudioClip _hover;
    [SerializeField] private AudioClip _click;

    private void Awake()
    {
        _awake?.Invoke();
    }

    public void PlayHover()
    {
        _source.PlayOneShot(_hover);
    }

    public void PlayClick()
    {
        _source.PlayOneShot(_click);
    }

    public void SetSource(AudioSource source)
    {
        _source = source;
    }
}