using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FinalBossDiamondFlickerOverlay : MonoBehaviour
{
    private const string DustSystemName = "Dust";
    private const string TwinkleSystemName = "Twinkle";
    private const string MoodSystemName = "Mood";

    [SerializeField] private GameObject calmPrefab;
    [SerializeField] private GameObject windPrefab;
    [SerializeField] private float calmDuration = 0.8f;
    [SerializeField] private float windDuration = 0.8f;

    private FlickerProfile calmProfile;
    private FlickerProfile windProfile;
    private Coroutine loopRoutine;

    private sealed class FlickerProfile
    {
        public GameObject Instance;
        public ParticleChannel Dust;
        public ParticleChannel Twinkle;
        public ParticleChannel Mood;
        public ParticleSystem[] AllSystems;
    }

    private sealed class ParticleChannel
    {
        public ParticleSystem System;
        public float BaseRateOverTime;
    }

    public void Configure(GameObject calmSourcePrefab, GameObject windSourcePrefab)
    {
        if (calmPrefab == calmSourcePrefab && windPrefab == windSourcePrefab)
        {
            return;
        }

        calmPrefab = calmSourcePrefab;
        windPrefab = windSourcePrefab;
        RebuildInstances();
    }

    public void BeginLoop()
    {
        if (!this) return;
        if (!EnsureInstances()) return;
        if (loopRoutine != null) return;

        ActivateProfiles();
        ApplyBlend(1f, 0f);
        loopRoutine = StartCoroutine(FlickerRoutine());
    }

    public void StopLoop()
    {
        if (!this) return;

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        StopAndClear(calmProfile);
        StopAndClear(windProfile);
    }

    private void OnDisable()
    {
        StopLoop();
    }

    private void OnDestroy()
    {
        DestroyProfile(ref calmProfile);
        DestroyProfile(ref windProfile);
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            yield return CrossFade(1f, 0f, 0f, 1f, Mathf.Max(0.01f, calmDuration));
            yield return CrossFade(0f, 1f, 1f, 0f, Mathf.Max(0.01f, windDuration));
        }
    }

    private IEnumerator CrossFade(float calmStart, float windStart, float calmEnd, float windEnd, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            float calmWeight = Mathf.Lerp(calmStart, calmEnd, smooth);
            float windWeight = Mathf.Lerp(windStart, windEnd, smooth);
            ApplyBlend(calmWeight, windWeight);
            yield return null;
        }

        ApplyBlend(calmEnd, windEnd);
    }

    private bool EnsureInstances()
    {
        if (calmProfile == null)
        {
            calmProfile = CreateProfile(calmPrefab, "DiamondFlicker_Calm");
        }

        if (windProfile == null)
        {
            windProfile = CreateProfile(windPrefab, "DiamondFlicker_Wind");
        }

        if (calmProfile == null || windProfile == null)
        {
            Debug.LogWarning("FinalBossDiamondFlickerOverlay: Calm/Wind prefabs are required to run the effect.", this);
            return false;
        }

        return true;
    }

    private void ActivateProfiles()
    {
        PlayProfile(calmProfile);
        PlayProfile(windProfile);
    }

    private void ApplyBlend(float calmWeight, float windWeight)
    {
        ApplyChannelWeight(calmProfile != null ? calmProfile.Dust : null, calmWeight);
        ApplyChannelWeight(calmProfile != null ? calmProfile.Twinkle : null, calmWeight);
        ApplyChannelWeight(windProfile != null ? windProfile.Dust : null, windWeight);
        ApplyChannelWeight(windProfile != null ? windProfile.Twinkle : null, windWeight);

        // Mood is identical in both prefabs, so keep only one always-on layer to avoid doubling the background.
        ApplyChannelWeight(calmProfile != null ? calmProfile.Mood : null, 1f);
        ApplyChannelWeight(windProfile != null ? windProfile.Mood : null, 0f);
    }

    private static void ApplyChannelWeight(ParticleChannel channel, float weight)
    {
        if (channel == null || channel.System == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = channel.System.emission;
        emission.enabled = weight > 0.001f;
        emission.rateOverTimeMultiplier = channel.BaseRateOverTime * Mathf.Clamp01(weight);
    }

    private FlickerProfile CreateProfile(GameObject prefab, string fallbackName)
    {
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, transform);
        instance.name = fallbackName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        Dictionary<string, ParticleSystem> systemsByName = new Dictionary<string, ParticleSystem>();
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null) continue;

            string systemName = system.gameObject.name;
            if (!systemsByName.ContainsKey(systemName))
            {
                systemsByName.Add(systemName, system);
            }
        }

        FlickerProfile profile = new FlickerProfile
        {
            Instance = instance,
            AllSystems = systems,
            Dust = CreateChannel(systemsByName, DustSystemName),
            Twinkle = CreateChannel(systemsByName, TwinkleSystemName),
            Mood = CreateChannel(systemsByName, MoodSystemName)
        };

        StopAndClear(profile);
        return profile;
    }

    private static ParticleChannel CreateChannel(Dictionary<string, ParticleSystem> systemsByName, string channelName)
    {
        if (systemsByName == null || !systemsByName.TryGetValue(channelName, out ParticleSystem system) || system == null)
        {
            return null;
        }

        return new ParticleChannel
        {
            System = system,
            BaseRateOverTime = system.emission.rateOverTimeMultiplier
        };
    }

    private static void PlayProfile(FlickerProfile profile)
    {
        if (profile == null || profile.Instance == null)
        {
            return;
        }

        if (!profile.Instance.activeSelf)
        {
            profile.Instance.SetActive(true);
        }

        PlayChannel(profile.Dust);
        PlayChannel(profile.Twinkle);
        PlayChannel(profile.Mood);
    }

    private static void StopAndClear(FlickerProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        ApplyChannelWeight(profile.Dust, 0f);
        ApplyChannelWeight(profile.Twinkle, 0f);
        ApplyChannelWeight(profile.Mood, 0f);

        if (profile.AllSystems != null)
        {
            for (int i = 0; i < profile.AllSystems.Length; i++)
            {
                ParticleSystem system = profile.AllSystems[i];
                if (system == null) continue;

                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (profile.Instance != null && profile.Instance.activeSelf)
        {
            profile.Instance.SetActive(false);
        }
    }

    private static void PlayChannel(ParticleChannel channel)
    {
        if (channel == null || channel.System == null)
        {
            return;
        }

        channel.System.Play(true);
    }

    private void RebuildInstances()
    {
        bool wasRunning = loopRoutine != null;
        if (wasRunning)
        {
            StopLoop();
        }

        DestroyProfile(ref calmProfile);
        DestroyProfile(ref windProfile);

        if (wasRunning)
        {
            BeginLoop();
        }
    }

    private static void DestroyProfile(ref FlickerProfile profile)
    {
        if (profile != null && profile.Instance != null)
        {
            Destroy(profile.Instance);
        }

        profile = null;
    }
}
