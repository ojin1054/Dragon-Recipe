using DragonRecipe.Core;
using DragonRecipe.Flight;
using UnityEngine;

namespace DragonRecipe.Care
{
    /// <summary>
    /// 비행 씬 ↔ 드래곤 상태 연결.
    /// 시작할 때 현재 성장 단계와 굶주림을 비행 수치에 반영하고,
    /// 비행이 끝나면 잡은 먹이를 먹이고 부상/성장을 저장한다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // DragonFlightController / FlightSession보다 먼저
    public class FlightCareBridge : MonoBehaviour
    {
        public GameConfig config;
        public DragonFlightController dragon;
        public FlightSession session;

        void Awake()
        {
            DragonCare.EnsureLoaded(config);
            ApplyStageToDragon();
        }

        void OnEnable() => GameEvents.FlightEnded += OnFlightEnded;
        void OnDisable() => GameEvents.FlightEnded -= OnFlightEnded;

        void Start()
        {
            if (session) session.StateChanged += OnSessionState;
        }

        void OnDestroy()
        {
            if (session) session.StateChanged -= OnSessionState;
            DragonCare.Save();
        }

        void OnSessionState(FlightSession.State s)
        {
            // 다시 날기 → 그 사이 성장/허기 변화를 반영
            if (s == FlightSession.State.Ready) ApplyStageToDragon();
        }

        void ApplyStageToDragon()
        {
            if (!dragon || !DragonCare.IsLoaded) return;
            var data = DragonCare.CurrentStageData;
            if (!data) return;
            dragon.SetStage(data, DragonCare.CurrentFlightParams(), CareStats.Current());
            // 자랄수록 동물이 드래곤을 늦게 알아챈다
            if (session && session.spawner) session.spawner.awarenessScale = data.preyAwarenessScale * DragonCare.Upgrades.preyAwareness;
        }

        void OnFlightEnded(FlightResult result) => DragonCare.ApplyFlightResult(result);

        void OnApplicationPause(bool paused)
        {
            if (paused) DragonCare.Save();
        }

        void OnApplicationQuit() => DragonCare.Save();
    }
}
