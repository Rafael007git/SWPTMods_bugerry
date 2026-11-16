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

        // Для морфа "74- Genesis8Female__mine breast rotation updown"
        public static ConfigEntry<bool> EnableMorph74;
        public static ConfigEntry<float> Morph74Sensitivity;
        public static ConfigEntry<float> Morph74MaxDelta;
        public static ConfigEntry<bool> OnlyWhenTopless;

        private class BoneState
        {
            public Transform Chest;
            public Vector3 BobWorldPos;
            public Vector3 BobWorldVel;
            public bool Initialized;
            public float LastAnglePitch;
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

            EnableMorph74 = config.Bind("Morph74Experiment", "EnableMorph74", true, "Подключить 74-й блендшейп при движении груди вверх");
            Morph74Sensitivity = config.Bind("Morph74Experiment", "Morph74Sensitivity", 4.0f, "Чувствительность морфа к углу подъёма (единиц веса на 1 градус)");
            Morph74MaxDelta = config.Bind("Morph74Experiment", "Morph74MaxDelta", 45.0f, "Максимальное увеличение веса морфа от базового (например, от 50 до 95)");
            OnlyWhenTopless = config.Bind("Morph74Experiment", "OnlyWhenTopless", true, "Использовать Morph74 только когда сняты armor и bra (защита от клиппинга)");
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

            float rawAnglePitch = Mathf.Asin(Mathf.Clamp(deltaY, -0.9f, 0.9f)) * Mathf.Rad2Deg;
            float anglePitch = Mathf.Clamp(rawAnglePitch, -MaxAngleDown.Value, MaxAngleUp.Value);
            float angleYaw = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(deltaX, -0.9f, 0.9f)) * Mathf.Rad2Deg, -MaxAngleSide.Value, MaxAngleSide.Value);

            Quaternion chestSpaceDelta = Quaternion.Euler(-anglePitch, angleYaw, 0f);
            bone.rotation = chest.rotation * chestSpaceDelta * Quaternion.Inverse(chest.rotation) * bone.rotation;

            // Передаём rawAnglePitch (не обрезанный лимитом MaxAngleUp), чтобы морф плавно работал на весь диапазон
            UpdateCharacterMorph74(jiggle, rawAnglePitch);
        }

        private static readonly Dictionary<SkinnedMeshRenderer, float> _baseMorph74 = new Dictionary<SkinnedMeshRenderer, float>();
        private const int TargetMorphIndex = 74;

        // Обновление Морфа-74
        private static void UpdateCharacterMorph74(Jiggle jiggle, float anglePitch)
        {
            if (EnableMorph74 == null || !EnableMorph74.Value) return;
            if (jiggle.name != "lPectoral") return; // Считаем один раз на персонажа за кадр

            var cc = jiggle.GetComponentInParent<CharacterCustomization>();
            if (cc == null || cc.body == null || cc.body.sharedMesh == null) return;

            SkinnedMeshRenderer bodySmr = cc.body;
            if (bodySmr.sharedMesh.blendShapeCount <= TargetMorphIndex) return;

            // Кэшируем исходный вес морфа из кастомизации персонажа (обычно 50)
            if (!_baseMorph74.TryGetValue(bodySmr, out float baseWeight))
            {
                baseWeight = bodySmr.GetBlendShapeWeight(TargetMorphIndex);
                _baseMorph74[bodySmr] = baseWeight;
            }

            // Если надеты armor или bra — возвращаем базовый вес и не трогаем морф (защита от клиппинга)
            if (OnlyWhenTopless != null && OnlyWhenTopless.Value && IsTopCovered(cc))
            {
                if (!Mathf.Approximately(bodySmr.GetBlendShapeWeight(TargetMorphIndex), baseWeight))
                {
                    bodySmr.SetBlendShapeWeight(TargetMorphIndex, baseWeight);
                }
                return;
            }

            // Реагируем только на положительный угол (подъём вверх)
            float upwardAngle = Mathf.Max(0f, anglePitch);
            float deltaWeight = Mathf.Clamp(upwardAngle * Morph74Sensitivity.Value, 0f, Morph74MaxDelta.Value);
            float finalWeight = Mathf.Clamp(baseWeight + deltaWeight, 0f, 100f);

            bodySmr.SetBlendShapeWeight(TargetMorphIndex, finalWeight);

            // Точечно синхронизируем 74-й индекс с оставшимися рендерерами (на случай украшений/аксессуаров)
            var appeal = cc.GetComponent<Appeal>();
            if (appeal != null && appeal.allRenderers != null)
            {
                foreach (var renderer in appeal.allRenderers)
                {
                    if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > TargetMorphIndex)
                    {
                        renderer.SetBlendShapeWeight(TargetMorphIndex, finalWeight);
                    }
                }
            }
        }

        public static void Unregister(Jiggle jiggle)
        {
            if (jiggle == null) return;

            _states.Remove(jiggle);

            // Находим меш тела персонажа и удаляем его из кэша базовых весов
            var cc = jiggle.GetComponentInParent<CharacterCustomization>();
            if (cc != null && cc.body != null)
            {
                _baseMorph74.Remove(cc.body);
            }

            // На случай, если CharacterCustomization уже уничтожен раньше кости — чистим все мёртвые ссылки
            var deadKeys = new List<SkinnedMeshRenderer>();
            foreach (var kvp in _baseMorph74)
            {
                if (kvp.Key == null) deadKeys.Add(kvp.Key);
            }
            foreach (var key in deadKeys)
            {
                _baseMorph74.Remove(key);
            }
        }

        private static bool IsTopCovered(CharacterCustomization cc)
        {
            if (cc == null) return false;

            bool armorActive = cc.armor != null && cc.armor.gameObject.activeInHierarchy;
            bool braActive = cc.bra != null && cc.bra.gameObject.activeInHierarchy;

            return armorActive || braActive;
        }

    }
}