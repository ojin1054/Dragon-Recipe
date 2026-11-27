using UnityEngine;

namespace DragonRecipe.Food
{
    /// <summary>지상에서 배회하다가 드래곤이 다가오면 도망가는 먹이.</summary>
    public class Prey : MonoBehaviour
    {
        public FoodData Data { get; private set; }
        public bool IsAvailable { get; private set; }

        Transform _threat;
        PreySpawner _spawner;
        Vector3 _wanderDir;
        float _nextTurn;
        float _groundY;

        public void Init(FoodData data, PreySpawner spawner, Transform threat)
        {
            Data = data;
            _spawner = spawner;
            _threat = threat;
        }

        public void Place(Vector3 groundPoint)
        {
            _groundY = groundPoint.y;
            transform.position = new Vector3(groundPoint.x, _groundY + Data.bodySize.y * 0.5f, groundPoint.z);
            transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            _wanderDir = transform.forward;
            _nextTurn = 0f;
            IsAvailable = true;
            gameObject.SetActive(true);
        }

        public void MarkCaught()
        {
            IsAvailable = false;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsAvailable || Data == null) return;

            Vector3 move;
            float speed;
            Vector3 toThreat = _threat ? _threat.position - transform.position : Vector3.positiveInfinity;
            Vector3 flatToThreat = new Vector3(toThreat.x, 0, toThreat.z);

            if (Data.flees && _threat && flatToThreat.magnitude < Data.fleeRadius && toThreat.y < Data.fleeRadius)
            {
                move = -flatToThreat.normalized;
                speed = Data.fleeSpeed;
            }
            else
            {
                if (Time.time >= _nextTurn)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    _wanderDir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    _nextTurn = Time.time + Random.Range(2f, 5f);
                }
                move = _wanderDir;
                speed = Data.wanderSpeed;
            }

            Vector3 next = transform.position + move * (speed * Time.deltaTime);
            if (_spawner) next = _spawner.ClampToArea(next);
            next.y = _groundY + Data.bodySize.y * 0.5f;
            transform.position = next;
            if (move.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 8f * Time.deltaTime);
        }
    }
}
