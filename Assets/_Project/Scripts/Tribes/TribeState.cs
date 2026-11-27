using System;
using System.Collections.Generic;

namespace DragonRecipe.Tribes
{
    public enum Tribe
    {
        Human,
        Orc,
        Elf
    }

    /// <summary>호감도 단계. 낮을수록 드래곤을 공격하고, 높을수록 제물과 축복을 준다</summary>
    public enum Standing
    {
        /// <summary>보이면 공격</summary>
        Hostile,
        /// <summary>자기 마을 땅에 들어오면 공격</summary>
        Wary,
        /// <summary>건드리지 않음</summary>
        Neutral,
        /// <summary>가끔 제물, 약한 축복</summary>
        Friendly,
        /// <summary>매번 제물, 강한 축복</summary>
        Revered
    }

    /// <summary>종족 하나의 기록 (저장됨)</summary>
    [Serializable]
    public class TribeRecord
    {
        public Tribe tribe;
        /// <summary>호감도 (-100 ~ 100)</summary>
        public float reputation;
        /// <summary>전쟁에 져서 병력이 줄어 있는 남은 비행 횟수</summary>
        public int weakenedFlights;
        /// <summary>드래곤에게 공격받은 총 횟수</summary>
        public int attacksReceived;
    }

    /// <summary>제단에 놓인 제물 (창고가 가득 차 아직 못 옮긴 것)</summary>
    [Serializable]
    public class Offering
    {
        public Tribe from;
        public string foodId;
        public bool rare;
    }

    public enum WarStatus
    {
        /// <summary>전쟁 없음</summary>
        None,
        /// <summary>전쟁 중, 드래곤은 관여하지 않음 (요청 없음)</summary>
        Bystander,
        /// <summary>참여 요청이 와서 답을 기다림</summary>
        Requested,
        /// <summary>한쪽 편으로 참전함</summary>
        Joined,
        /// <summary>요청을 거절함</summary>
        Declined
    }

    /// <summary>지금 벌어지는 전쟁 (저장됨). 한 번에 하나만</summary>
    [Serializable]
    public class WarState
    {
        public WarStatus status = WarStatus.None;
        public Tribe sideA;
        public Tribe sideB;
        /// <summary>sideA가 드래곤에게 참여를 요청함</summary>
        public bool requestA;
        public bool requestB;
        /// <summary>참전한 편</summary>
        public Tribe joined;
        /// <summary>남은 비행 횟수. 0이 되면 승패가 난다</summary>
        public int flightsLeft;
        /// <summary>참전한 뒤 적 편을 공격한 횟수 (승률을 올린다)</summary>
        public float contribution;
        /// <summary>다음 전쟁이 일어날 수 있을 때까지 남은 비행 횟수</summary>
        public int cooldown;

        public bool Active => status != WarStatus.None;
        public bool Involves(Tribe t) => Active && (sideA == t || sideB == t);
        public Tribe Enemy(Tribe side) => side == sideA ? sideB : sideA;
        public bool Requested(Tribe t) => t == sideA ? requestA : t == sideB && requestB;
    }

    /// <summary>세 종족과 드래곤의 관계 전부 (DragonState에 저장. 새 알을 부화시켜도 이어진다)</summary>
    [Serializable]
    public class TribeState
    {
        public List<TribeRecord> tribes = new List<TribeRecord>();
        public List<Offering> altar = new List<Offering>();
        public WarState war = new WarState();
        /// <summary>전쟁에서 이긴 편이 주는 축복의 남은 비행 횟수</summary>
        public int victoryFlightsLeft;
        public Tribe victoryFrom;

        public TribeRecord Get(Tribe t)
        {
            foreach (var r in tribes)
                if (r.tribe == t) return r;
            return null;
        }
    }

    /// <summary>비행 한 번 뒤 종족 쪽에서 일어난 일 (둥지 화면 연출용, 저장 안 됨)</summary>
    public class TribeReport
    {
        public readonly float[] repBefore = new float[3];
        public readonly float[] repAfter = new float[3];
        public readonly List<Offering> offerings = new List<Offering>();
        public bool warStarted;
        public bool warEnded;
        /// <summary>참전한 전쟁의 결과 (참전 안 했으면 의미 없음)</summary>
        public bool warJoined;
        public bool warWon;
        public Tribe warWinner;
        public Tribe warLoser;

        public float Delta(Tribe t) => repAfter[(int)t] - repBefore[(int)t];
    }
}
