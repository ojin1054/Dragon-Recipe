using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.CameraRig
{
    /// <summary>
    /// 빠르게 날수록 커지는 속도감 연출 (문구 없이 화면으로만).
    /// - 바람 줄기: 화면 가장자리로 하얀 줄기가 스쳐 지나간다. 빠를수록 많고 길고 진하다.
    /// - 카메라 떨림: 일정 속도를 넘으면 잔잔하게 떨리고, 빠를수록 세진다.
    /// - 날개 끝 비행운: 빠를 때 양 날개 끝에서 흰 꼬리가 뻗는다.
    /// - 속도 단계: 정해 둔 속도를 넘는 순간 시야각이 '펑' 넓어지고, 카메라가 툭 튀고, 바람 줄기가 한꺼번에 쏟아진다.
    /// <see cref="FollowCamera"/>에 자동으로 붙는다.
    /// </summary>
    [RequireComponent(typeof(FollowCamera))]
    public class SpeedJuice : MonoBehaviour
    {
        [Header("속도 구간 (드래곤 최고 속도 대비 비율)")]
        [Tooltip("이 비율부터 연출이 시작된다")]
        public float startRatio = 0.55f;
        [Tooltip("이 비율에서 연출이 가장 세다")]
        public float fullRatio = 1f;
        [Tooltip("이 비율들을 넘는 순간 한 번씩 '펑' (낮은 순서)")]
        public float[] tierRatios = { 0.75f, 0.95f };
        [Tooltip("단계를 넘은 뒤 최고 속도의 이 비율만큼 느려져야 다시 '펑'할 수 있다")]
        public float tierHysteresis = 0.1f;

        [Header("바람 줄기")]
        public bool windStreaks = true;
        public float maxStreakRate = 70f;
        public int tierBurst = 24;
        [Tooltip("카메라 앞 이 거리에서 생겨서 카메라 쪽으로 지나간다")]
        public float streakDistance = 26f;
        [Tooltip("화면 가운데(드래곤)를 비워 두는 반지름")]
        public float streakRadius = 9f;

        [Header("카메라 떨림")]
        public float maxShake = 0.12f;
        public float maxShakeRoll = 0.8f;
        public float shakeFrequency = 22f;
        public float tierKickShake = 0.35f;

        [Header("시야각 펀치")]
        public float tierFovPunch = 7f;
        public float punchDecay = 4f;

        [Header("날개 끝 비행운")]
        public bool contrails = true;
        [Tooltip("연출 세기(0~1)가 이 값을 넘으면 비행운이 나온다")]
        public float contrailFrom = 0.45f;
        public float contrailTime = 0.35f;
        public float contrailWidth = 0.12f;

        /// <summary>지금 연출 세기 (0 = 보통, 1 = 최고 속도)</summary>
        public float Intensity { get; private set; }
        /// <summary>지금까지 넘은 속도 단계 수 (이번 비행 중 '펑' 횟수)</summary>
        public int TierHits { get; private set; }
        /// <summary>지금 속도로 도달해 있는 단계 (0 = 아직 아무 단계도 아님)</summary>
        public int CurrentTier { get; private set; }

        public ParticleSystem Streaks => _streaks;
        public TrailRenderer LeftContrail => _trailL;
        public TrailRenderer RightContrail => _trailR;

        FollowCamera _follow;
        ParticleSystem _streaks;
        TrailRenderer _trailL;
        TrailRenderer _trailR;
        DragonVisual _visual;
        float _kick;
        float _punch;
        float _seed;
        static Material _mat;

        void Awake()
        {
            _follow = GetComponent<FollowCamera>();
            _seed = Random.value * 100f;
            if (windStreaks) _streaks = BuildStreaks();
        }

        void Update()
        {
            var dragon = _follow ? _follow.target : null;
            bool flying = dragon && dragon.IsFlying && dragon.Model != null;
            float speed = flying ? dragon.Model.Velocity.magnitude : 0f;
            float top = flying ? Mathf.Max(1f, dragon.Model.Params.maxSpeed) : 1f;
            float ratio = speed / top;
            float dt = Time.deltaTime;

            float goal = Mathf.Clamp01(Mathf.InverseLerp(startRatio, fullRatio, ratio));
            // 올라갈 때는 빠르게, 내려갈 때는 천천히 풀린다
            float rate = goal > Intensity ? 6f : 2.5f;
            Intensity = Mathf.MoveTowards(Intensity, goal, rate * dt);
            if (!flying) Intensity = 0f;

            UpdateTiers(ratio, flying);
            UpdateStreaks();
            UpdateShake(dt);
            UpdateContrails(dragon, flying);
        }

        void UpdateTiers(float ratio, bool flying)
        {
            if (!flying)
            {
                CurrentTier = 0;
                return;
            }
            // 떨어질 때: 단계 속도보다 충분히 느려지면 그 단계를 다시 열어 둔다
            while (CurrentTier > 0 && ratio < tierRatios[CurrentTier - 1] - tierHysteresis)
                CurrentTier--;
            // 올라갈 때: 넘은 단계마다 '펑'
            while (CurrentTier < tierRatios.Length && ratio >= tierRatios[CurrentTier])
            {
                CurrentTier++;
                TierHits++;
                float strength = (float)CurrentTier / Mathf.Max(1, tierRatios.Length);
                _kick = Mathf.Max(_kick, tierKickShake * Mathf.Lerp(0.6f, 1f, strength));
                _punch = Mathf.Max(_punch, tierFovPunch * Mathf.Lerp(0.6f, 1f, strength));
                if (_streaks) _streaks.Emit(Mathf.RoundToInt(tierBurst * Mathf.Lerp(0.6f, 1f, strength)));
            }
        }

        void UpdateStreaks()
        {
            if (!_streaks) return;
            var em = _streaks.emission;
            em.rateOverTime = maxStreakRate * Intensity * Intensity;
            var main = _streaks.main;
            main.startColor = new Color(1f, 1f, 1f, Mathf.Lerp(0.2f, 0.55f, Intensity));
            var r = _streaks.GetComponent<ParticleSystemRenderer>();
            r.velocityScale = Mathf.Lerp(0.04f, 0.1f, Intensity);
        }

        void UpdateShake(float dt)
        {
            float t = Time.time * shakeFrequency + _seed;
            float n1 = Mathf.PerlinNoise(t, 0.3f) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(0.7f, t) * 2f - 1f;
            float n3 = Mathf.PerlinNoise(t * 0.6f, t * 0.6f + 5f) * 2f - 1f;
            float amp = maxShake * Intensity * Intensity + _kick;
            _follow.ShakeOffset = new Vector3(n1, n2, 0f) * amp;
            _follow.ShakeRoll = n3 * (maxShakeRoll * Intensity * Intensity + _kick * 4f);
            _follow.ExtraFov = _punch;

            _kick = Mathf.MoveTowards(_kick, 0f, dt * 1.5f);
            _punch = Mathf.Lerp(_punch, 0f, 1f - Mathf.Exp(-punchDecay * dt));
            if (_punch < 0.01f) _punch = 0f;
        }

        void UpdateContrails(DragonFlightController dragon, bool flying)
        {
            if (!contrails || !dragon) return;
            if (!_visual) _visual = dragon.GetComponent<DragonVisual>();
            if (!_visual) return;
            if (!_trailL && _visual.leftWing) _trailL = BuildTrail(_visual.leftWing, "ContrailL");
            if (!_trailR && _visual.rightWing) _trailR = BuildTrail(_visual.rightWing, "ContrailR");

            float k = Mathf.InverseLerp(contrailFrom, 1f, Intensity);
            bool on = flying && Intensity > contrailFrom;
            float scale = Mathf.Max(0.1f, dragon.transform.localScale.x);
            foreach (var trail in new[] { _trailL, _trailR })
            {
                if (!trail) continue;
                trail.emitting = on;
                trail.widthMultiplier = contrailWidth * scale * Mathf.Lerp(0.5f, 1f, k);
                trail.time = contrailTime * Mathf.Lerp(0.5f, 1f, k);
                trail.startColor = new Color(1f, 1f, 1f, 0.55f * Mathf.Max(0.3f, k));
                if (!flying) trail.Clear();
            }
        }

        static Material Mat
        {
            get
            {
                if (_mat) return _mat;
                var shader = Shader.Find("Sprites/Default");
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
                _mat = new Material(shader) { name = "SpeedJuice" };
                return _mat;
            }
        }

        TrailRenderer BuildTrail(Transform wing, string name)
        {
            // 날개 끝 (없으면 날개 자체)
            var tip = wing.Find("Tip") ?? wing;
            var go = new GameObject(name);
            go.transform.SetParent(tip, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Mat;
            trail.time = contrailTime;
            trail.minVertexDistance = 0.3f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.startColor = new Color(1f, 1f, 1f, 0.55f);
            trail.endColor = new Color(1f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return trail;
        }

        ParticleSystem BuildStreaks()
        {
            var go = new GameObject("WindStreaks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, streakDistance);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 카메라 쪽으로 날아온다

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(42f, 58f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.maxParticles = 160;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var em = ps.emission;
            em.rateOverTime = 0f;

            // 가장자리 고리에서만 생겨 화면 가운데(드래곤)는 비워 둔다
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = streakRadius;
            shape.radiusThickness = 0.35f;
            shape.arc = 360f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 2f;
            r.sharedMaterial = Mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            return ps;
        }
    }
}
