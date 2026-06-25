using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class HandReceiver : MonoBehaviour
{
    [Header("TCP 설정")]
    public string host = "127.0.0.1";
    public int port = 5005;

    [Header("손 시각화 설정")]
    public float sphereSize  = 0.05f;
    public float scaleXY     = 3f;
    public float scaleZ      = 1f;
    public float handDepth   = 3f;
    [Tooltip("클수록 빠르게 따라옴 (10~30 추천)")]
    public float smoothSpeed = 20f;

    // 바이너리 프로토콜 상수
    // MAGIC(2) + 21개 × xyz × 4bytes = 254 bytes
    private const int  PACKET_SIZE  = 254;
    private const byte MAGIC_0      = 0xAB;
    private const byte MAGIC_1      = 0xCD;

    private readonly Color[] fingerColors = new Color[]
    {
        Color.red, Color.green, Color.blue, Color.yellow, Color.cyan
    };
    private static readonly int[] LandmarkFinger = new int[]
    {
        0, 0,0,0,0, 1,1,1,1, 2,2,2,2, 3,3,3,3, 4,4,4,4
    };

    private Transform[]   _joints;
    private Camera        _cam;
    private TcpClient     _client;
    private NetworkStream _stream;
    private Thread        _recvThread;
    private volatile bool _running = false;

    // 스레드 간 공유: float[21×3]
    private readonly object _lock       = new();
    private float[]         _pendingXYZ = null;

    void Awake()
    {
        _joints = new Transform[21];
        for (int i = 0; i < 21; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Joint_{i:D2}";
            go.transform.parent        = this.transform;
            go.transform.localScale    = Vector3.one * sphereSize;
            go.transform.localPosition = Vector3.zero;

            var rend = go.GetComponent<Renderer>();
            var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = fingerColors[LandmarkFinger[i]];
            rend.material = mat;

            Destroy(go.GetComponent<Collider>());
            _joints[i] = go.transform;
        }
        Debug.Log("[HandReceiver] Sphere 21개 생성 완료");
    }

    void Start()
    {
        _cam = Camera.main;
        Connect();
    }

    void Update()
    {
        float[] xyz = null;
        lock (_lock)
        {
            if (_pendingXYZ != null)
            {
                xyz = _pendingXYZ;
                _pendingXYZ = null;
            }
        }
        if (xyz != null)
            ApplyXYZ(xyz);
    }

    void OnDestroy() => Disconnect();

    public Transform[] GetJoints() => _joints;

    void Connect()
    {
        try
        {
            _client = new TcpClient(host, port);
            // Nagle 알고리즘 비활성화 → 소량 패킷 즉시 전송
            _client.NoDelay = true;
            _stream  = _client.GetStream();
            _running = true;
            _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
            _recvThread.Start();
            Debug.Log("[HandReceiver] Python 연결 성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[HandReceiver] 연결 실패 → Python 먼저 실행하세요! ({e.Message})");
        }
    }

    void Disconnect()
    {
        _running = false;
        _stream?.Close();
        _client?.Close();
        _recvThread?.Join(500);
    }

    // ── 수신 루프: 바이너리 254바이트 패킷 읽기 ─────────────────────────────
    void ReceiveLoop()
    {
        var  recvBuf  = new byte[PACKET_SIZE * 4]; // 여유있게 4배
        var  partial  = new byte[PACKET_SIZE * 8]; // 누적 버퍼
        int  partialLen = 0;

        while (_running)
        {
            try
            {
                int n = _stream.Read(recvBuf, 0, recvBuf.Length);
                if (n == 0) break;

                // partial 버퍼에 누적
                Buffer.BlockCopy(recvBuf, 0, partial, partialLen, n);
                partialLen += n;

                // 완성된 패킷 처리
                int offset = 0;
                while (partialLen - offset >= PACKET_SIZE)
                {
                    // 매직 넘버 확인
                    if (partial[offset] == MAGIC_0 && partial[offset + 1] == MAGIC_1)
                    {
                        var xyz = new float[63]; // 21 × 3
                        for (int i = 0; i < 63; i++)
                        {
                            // Big-endian float 변환
                            int b = offset + 2 + i * 4;
                            if (BitConverter.IsLittleEndian)
                            {
                                byte b0 = partial[b], b1 = partial[b+1],
                                     b2 = partial[b+2], b3 = partial[b+3];
                                xyz[i] = BitConverter.ToSingle(
                                    new byte[] { b3, b2, b1, b0 }, 0);
                            }
                            else
                            {
                                xyz[i] = BitConverter.ToSingle(partial, b);
                            }
                        }
                        lock (_lock) { _pendingXYZ = xyz; }
                        offset += PACKET_SIZE;
                    }
                    else
                    {
                        offset++; // 동기 맞추기
                    }
                }

                // 처리 못한 잔여 데이터 앞으로 이동
                if (offset > 0)
                {
                    partialLen -= offset;
                    Buffer.BlockCopy(partial, offset, partial, 0, partialLen);
                }
            }
            catch (Exception e)
            {
                if (_running)
                    Debug.LogWarning($"[HandReceiver] 수신 오류: {e.Message}");
                break;
            }
        }
    }

    void ApplyXYZ(float[] xyz)
    {
        float t = Time.deltaTime * smoothSpeed;  // Lerp 계수

        // 손목 월드 위치 → Lerp로 부드럽게 이동
        float wx = xyz[0], wy = xyz[1];
        Vector3 targetWrist = _cam.ViewportToWorldPoint(
            new Vector3(wx, 1f - wy, handDepth)
        );
        this.transform.position = Vector3.Lerp(this.transform.position, targetWrist, t);

        float refX = xyz[0] * scaleXY;
        float refY = -xyz[1] * scaleXY;
        float refZ = xyz[2] * scaleZ;

        for (int i = 0; i < 21; i++)
        {
            if (_joints[i] == null) continue;
            Vector3 targetLocal = new Vector3(
                xyz[i*3  ] * scaleXY - refX,
               -xyz[i*3+1] * scaleXY - refY,
                xyz[i*3+2] * scaleZ  - refZ
            );
            // 관절도 Lerp로 부드럽게 이동
            _joints[i].localPosition = Vector3.Lerp(_joints[i].localPosition, targetLocal, t);
        }
    }
}
