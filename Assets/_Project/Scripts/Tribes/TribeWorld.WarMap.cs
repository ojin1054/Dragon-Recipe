using UnityEngine;

namespace DragonRecipe.Tribes
{
    /// <summary>
    /// 전쟁 맵(WarMap이 있는 씬)일 때의 배치.
    /// - 세 마을 대신 편든 쪽 진영(절벽 아래)과 적 진영(반대편 끝)만 세운다.
    /// - 적 무리는 적 진영 앞에서 여러 줄로 늘어서서 편든 쪽 진영으로 행군한다. 무리마다 더 많아진다.
    /// </summary>
    public partial class TribeWorld
    {
        WarMap _warMap;
        int _reachedSinceBreach;

        /// <summary>지금 전쟁 맵에서 날고 있는지</summary>
        public bool OnWarMap => _warMap;

        void BuildWarCamps()
        {
            var war = State.war;
            Tribe ally = war.joined;
            Tribe enemy = war.Enemy(ally);
            BuildVillage(ally, _warMap.allyCamp);
            BuildVillage(enemy, _warMap.enemyCamp);
            _warMap.Paint(ally, enemy);
            foreach (var v in _villages)
                if (v != null) v.shownRep = TribeLogic.Reputation(State, v.tribe);
            Populate();
        }

        void SpawnWarWave(Tribe ally, Tribe enemy, int wave)
        {
            var a = _villages[(int)ally];
            var e = _villages[(int)enemy];
            if (a == null || e == null) return;
            var s = Settings;

            Vector3 dir = a.center - e.center; // 적 진영 → 편든 쪽 진영
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            // 무리마다 가운데·왼쪽·오른쪽을 번갈아 출발
            Vector3 front = e.center + dir * s.warSpawnAhead + side * (((wave % 3) - 1) * 18f);

            int n = TribeLogic.WarRaidSize(wave, s);
            int width = Mathf.Max(1, s.warRankWidth);
            for (int i = 0; i < n; i++)
            {
                int row = i / width;
                int inRow = Mathf.Min(width, n - row * width);
                float lateral = ((i % width) - (inRow - 1) * 0.5f) * 2.4f + Random.Range(-0.5f, 0.5f);
                float back = row * 2.8f + Random.Range(0f, 0.8f);
                var m = Spawn(e, front + side * lateral - dir * back, true, false);
                // 목표: 편든 쪽 진영 가운데 (옆으로 조금씩 퍼져서)
                if (m) MakeRaider(m, a.center + side * (lateral * 0.5f));
            }
        }

        /// <summary>침입자가 진영에 닿음. 전쟁 맵은 군대가 크니 여러 명이 닿아야 한 번 뚫린 것으로 센다</summary>
        void RaiderReached(Tribe ally)
        {
            if (_warMap && ++_reachedSinceBreach < Mathf.Max(1, Settings.warRaidersPerBreach)) return;
            _reachedSinceBreach = 0;
            Breach(ally);
        }

        /// <summary>병사를 침입자로: 목표 지점으로 행군하며 드래곤을 쏜다. 멀리서도 보이게 횃불을 든다</summary>
        void MakeRaider(TribeMember m, Vector3 target)
        {
            m.IsRaider = true;
            m.home = target;
            m.reload = Random.Range(1f, 3f);
            var torch = Part(PrimitiveType.Cube, "Torch", m.transform, new Vector3(-0.35f, 2.3f, 0.1f), new Vector3(0.22f, 0.35f, 0.22f), new Color(1f, 0.55f, 0.1f));
            torch.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
