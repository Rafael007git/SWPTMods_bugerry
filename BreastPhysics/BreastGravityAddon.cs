using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using Assets.DuckType.Jiggle;

namespace BreastPhysics
{
    public static class BreastGravityAddon
    {
        public static ConfigEntry<bool> EnableAddon;
        public static ConfigEntry<bool> DisableVanillaJiggle;
        public static ConfigEntry<float> GravityStrength;
        public static ConfigEntry<float> InertiaStrength;
        public static ConfigEntry<float> VerticalStiffness;
        public static ConfigEntry<float> VerticalDamping;
        public static ConfigEntry<float> MaxAngleDown;
        public static ConfigEntry<float> MaxAngleUp;
        public static ConfigEntry<float> MaxAngleSide;
        public static ConfigEntry<bool> CompensateStandingPose;

        private class BoneState
        {
            public Transform Chest;
            public Vector3 BobWorldPos;
            public Vector3 BobWorldVel;
            public bool Initialized;
        }

        private static readonly Dictionary<Jiggle, BoneState> _states = new Dictionary<Jiggle, BoneState>();
        private const float LeverLength = 0.12f;

        public static void Initialize(ConfigFile config)
        {
            EnableAddon = config.Bind("GravityAddon", "EnableAddon", true, "Включить гравитацию и линейную инерцию");
            DisableVanillaJiggle = config.Bind("GravityAddon", "DisableVanillaJiggle", false, "Отключить штатный DuckType.Jiggle (для теста)");

            GravityStrength = config.Bind("GravityAddon", "GravityStrength", 1.0f, "Сила гравитации (0 = выкл, 1 = норма)");
            InertiaStrength = config.Bind("GravityAddon", "InertiaStrength", 0.0f, "Сила линейной инерции (0 = только гравитация без качания, 1 = полная физика)");

            VerticalStiffness = config.Bind("GravityAddon", "VerticalStiffness", 180.0f, "Жёсткость пружины");
            VerticalDamping = config.Bind("GravityAddon", "VerticalDamping", 10.0f, "Затухание колебаний");

            MaxAngleDown = config.Bind("GravityAddon", "MaxAngleDown", 14.0f, "Макс. угол отвисания вниз/вперёд (град.)");
            MaxAngleUp = config.Bind("GravityAddon", "MaxAngleUp", 10.0f, "Макс. угол подскока вверх (град.)");
            MaxAngleSide = config.Bind("GravityAddon", "MaxAngleSide", 5.0f, "Макс. боковой сдвиг от гравитации (град.)");

            CompensateStandingPose = config.Bind("GravityAddon", "CompensateStandingPose", true, "Вычитать гравитацию позы стоя (чтобы грудь не опускалась ниже исходного меша, когда персонаж стоит прямо)");
        }

        public static void Unregister(Jiggle jiggle)
        {
            if (jiggle != null) _states.Remove(jiggle);
        }

        public static void ProcessBone(Jiggle jiggle, float dt)
        {
            if (EnableAddon == null || !EnableAddon.Value || jiggle == null) return;
            if (!jiggle.name.EndsWith("Pectoral")) return;

            Transform bone = jiggle.transform;
            Transform chest = bone.parent;
            if (chest == null) return;

            // Если включен тестовый режим отключения ванильного Jiggle — сбрасываем его поворот перед расчетом
            if (DisableVanillaJiggle.Value)
            {
                bone.localRotation = Quaternion.identity;
            }

            if (!_states.TryGetValue(jiggle, out BoneState state))
            {
                state = new BoneState { Chest = chest };
                _states[jiggle] = state;
            }

            dt = Mathf.Clamp(dt, 0.0001f, 0.033f);
            Vector3 targetPos = bone.position + chest.forward * LeverLength;

            if (!state.Initialized || state.Chest != chest || Vector3.Distance(targetPos, state.BobWorldPos) > 0.4f)
            {
                state.Chest = chest;
                state.BobWorldPos = targetPos;
                state.BobWorldVel = Vector3.zero;
                state.Initialized = true;
                return;
            }

            float gravStrength = GravityStrength.Value;
            float inertiaStrength = InertiaStrength.Value;
            float stiffness = VerticalStiffness.Value;
            float damping = VerticalDamping.Value;

            // Если включена компенсация позы стоя, вычитаем базовый вектор -chest.up
            Vector3 effectiveGravityDir = CompensateStandingPose.Value
                ? (Vector3.down + chest.up)
                : Vector3.down;

            Vector3 gravityOffset = effectiveGravityDir * (0.022f * gravStrength * (180f / Mathf.Max(stiffness, 20f)));
            Vector3 equilibriumPos = targetPos + gravityOffset;

            if (inertiaStrength <= 0.001f)
            {
                state.BobWorldPos = equilibriumPos;
                state.BobWorldVel = Vector3.zero;
            }
            else
            {
                Vector3 displacement = equilibriumPos - state.BobWorldPos;
                Vector3 accel = (displacement * stiffness) - (state.BobWorldVel * damping);
                state.BobWorldVel += accel * dt;
                state.BobWorldPos += state.BobWorldVel * dt;

                state.BobWorldPos = Vector3.Lerp(equilibriumPos, state.BobWorldPos, Mathf.Clamp01(inertiaStrength));
            }

            Vector3 localRestDir = chest.InverseTransformPoint(targetPos) - chest.InverseTransformPoint(bone.position);
            Vector3 localCurrentDir = chest.InverseTransformPoint(state.BobWorldPos) - chest.InverseTransformPoint(bone.position);

            float deltaY = (localCurrentDir.y - localRestDir.y) / LeverLength;
            float deltaX = (localCurrentDir.x - localRestDir.x) / LeverLength;

            float anglePitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(deltaY, -0.9f, 0.9f)) * Mathf.Rad2Deg, -MaxAngleDown.Value, MaxAngleUp.Value);
            float angleYaw = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(deltaX, -0.9f, 0.9f)) * Mathf.Rad2Deg, -MaxAngleSide.Value, MaxAngleSide.Value);

            Quaternion chestSpaceDelta = Quaternion.Euler(-anglePitch, angleYaw, 0f);
            bone.rotation = chest.rotation * chestSpaceDelta * Quaternion.Inverse(chest.rotation) * bone.rotation;
        }
    }
}