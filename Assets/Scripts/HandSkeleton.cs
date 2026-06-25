/*
 * HandSkeleton.cs
 * ---------------
 * HandReceiver의 Joint Transform들을 LineRenderer로 연결해서
 * Unity에서도 손 뼈대가 보이게 함
 *
 * 사용법:
 *   HandManager GameObject에 이 스크립트도 추가
 *   (HandReceiver와 같은 오브젝트에 붙이면 됨)
 */

using UnityEngine;

[RequireComponent(typeof(HandReceiver))]
public class HandSkeleton : MonoBehaviour
{
    [Header("선 굵기")]
    public float lineWidth = 0.01f;

    // 손가락 연결 정보 (랜드마크 인덱스 쌍)
    private static readonly (int, int)[] Connections = new (int, int)[]
    {
        // 엄지
        (0,1),(1,2),(2,3),(3,4),
        // 검지
        (0,5),(5,6),(6,7),(7,8),
        // 중지
        (0,9),(9,10),(10,11),(11,12),
        // 약지
        (0,13),(13,14),(14,15),(15,16),
        // 소지
        (0,17),(17,18),(18,19),(19,20),
        // 손바닥
        (5,9),(9,13),(13,17),(0,17),
    };

    private static readonly Color[] FingerColors = new Color[]
    {
        Color.red, Color.green, new Color(1f,0.65f,0f), Color.magenta, Color.cyan
    };

    private static readonly int[] LandmarkFinger = new int[]
    {
        0, 0,0,0,0, 1,1,1,1, 2,2,2,2, 3,3,3,3, 4,4,4,4
    };

    private LineRenderer[] _lines;
    private Transform[]    _joints;

    void Start()
    {
        _joints = GetComponent<HandReceiver>().GetJoints();
        _lines  = new LineRenderer[Connections.Length];

        for (int i = 0; i < Connections.Length; i++)
        {
            var go = new GameObject($"Bone_{i:D2}");
            go.transform.parent = this.transform;

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth    = lineWidth;
            lr.endWidth      = lineWidth;
            lr.useWorldSpace = true;

            // 손가락 색상 (시작 관절 기준)
            var col   = FingerColors[LandmarkFinger[Connections[i].Item1]];
            var mat   = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = col;
            lr.material = mat;

            _lines[i] = lr;
        }
    }

    void Update()
    {
        if (_joints == null) return;
        for (int i = 0; i < Connections.Length; i++)
        {
            var (a, b) = Connections[i];
            if (_joints[a] == null || _joints[b] == null) continue;
            _lines[i].SetPosition(0, _joints[a].position);
            _lines[i].SetPosition(1, _joints[b].position);
        }
    }
}
