using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShaderController : MonoBehaviour
{
    public Material targetMaterial;

    [Header("Speed")]
    public float transitionSpeed = 2f;

    [Header("Presets")]
    public ShaderPreset[] shaderPresets;

    Dictionary<string, Coroutine> floatRoutines = new Dictionary<string, Coroutine>();
    Dictionary<string, Coroutine> colorRoutines = new Dictionary<string, Coroutine>();

    private void OnApplicationQuit()
    {
        SetShaderPreset(2);
    }

    private void Awake()
    {
        SetShaderPreset(2);
    }

    public void SetFloat(string property, float value, bool smooth = true)
    {
        if (!smooth)
        {
            targetMaterial.SetFloat(property, value);
            return;
        }

        StartTransitionFloat(property, value);
    }

    public void SetColor(string property, Color value, bool smooth = true)
    {
        if (!smooth)
        {
            targetMaterial.SetColor(property, value);
            return;
        }

        StartTransitionColor(property, value);
    }

    void StartTransitionFloat(string property, float target)
    {
        if (floatRoutines.ContainsKey(property))
        {
            StopCoroutine(floatRoutines[property]);
        }

        floatRoutines[property] = StartCoroutine(LerpFloat(property, target));
    }

    void StartTransitionColor(string property, Color target)
    {
        if (colorRoutines.ContainsKey(property))
        {
            StopCoroutine(colorRoutines[property]);
        }

        colorRoutines[property] = StartCoroutine(LerpColor(property, target));
    }

    IEnumerator LerpFloat(string property, float target)
    {
        float start = targetMaterial.GetFloat(property);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;
            float value = Mathf.Lerp(start, target, t);
            targetMaterial.SetFloat(property, value);
            yield return null;
        }

        targetMaterial.SetFloat(property, target);
    }

    IEnumerator LerpColor(string property, Color target)
    {
        Color start = targetMaterial.GetColor(property);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;
            Color value = Color.Lerp(start, target, t);
            targetMaterial.SetColor(property, value);
            yield return null;
        }

        targetMaterial.SetColor(property, target);
    }

    public void SetShaderPreset(int presetID)
    {
        if (presetID >= shaderPresets.Length)
        {
            Debug.Log("Shader Preset couldn't be set - index is out of range.");
            return;
        }

        ShaderPreset sp = shaderPresets[presetID];

        if (sp.setFloat)
        {
            SetFloat(sp.floatProperty, sp.floatValue, sp.floatSmooth);
        }

        if (sp.setColor)
        {
            SetColor("_Color1", sp.color1, sp.colorSmooth);
            SetColor("_Color2", sp.color2, sp.colorSmooth);
            SetColor("_Color3", sp.color3, sp.colorSmooth);
        }
    }
}

[System.Serializable]
public class ShaderPreset
{
    [Header("Float")]
    public bool setFloat;
    public string floatProperty;
    public float floatValue;
    public bool floatSmooth = true;

    [Space, Header("Color")]
    public bool setColor;
    public Color color1 = new Color(0.871f, 0.267f, 0.231f, 1);
    public Color color2 = new Color(0, 0.42f, 0.706f, 1);
    public Color color3 = new Color(0.086f, 0.137f, 0.145f, 1);
    public bool colorSmooth = true;
}