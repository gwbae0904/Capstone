# 근전도 신호와 제어를 활용한 VR 촉각 구현 시스템
**팀명: 고잉메리호** | 건국대학교 전기전자공학부 | 2026 캡스톤디자인

---

## 프로젝트 개요
노트북 웹캠으로 손의 움직임을 추적하고, 사용자의 근육 신호(EMG)를 읽어 다섯 개의 손가락에 개별적인 물리적 피드백을 제공하는 VR 촉각 글러브 시스템입니다.

```
웹캠 → Python (MediaPipe) → TCP/IP → Unity (3D 렌더링)
                                           ↓
EMG 신호 (Arduino) → Raspberry Pi → 서보모터 (포스 피드백)
```

---

## 개발 환경
| 항목 | 버전 |
|------|------|
| Unity | 6000.3.18f1 (URP) |
| Python | 3.13+ |
| mediapipe | 0.10.35 |
| opencv-python | 4.13.0+ |

---

## 초기 세팅 방법

### 1. 저장소 클론
```bash
git clone https://github.com/[팀저장소주소].git
cd [프로젝트폴더]
```

### 2. Python 설치
👉 https://www.python.org/downloads/

> ⚠️ 설치 시 **"Add Python to PATH"** 반드시 체크!

### 3. Python 라이브러리 설치
```bash
pip install mediapipe opencv-python
```

### 4. MediaPipe 모델 파일 다운로드
`hand_landmarker.task` 파일은 용량 문제로 저장소에 포함되지 않습니다.
프로젝트 루트 폴더에 직접 다운로드하세요.

```bash
# 방법 1: 브라우저에서 직접 다운로드
# 아래 URL을 브라우저 주소창에 붙여넣기
https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task
```

```bash
# 방법 2: Python으로 자동 다운로드 (첫 실행 시 자동)
python hand_tracker.py  # 첫 실행 시 자동으로 다운로드됨
```

다운로드 후 파일 위치:
```
VR/                        ← Unity 프로젝트 루트
├── Assets/
├── hand_tracker.py        ✅ (저장소에 포함)
└── hand_landmarker.task   ← 여기에 위치해야 함 (직접 다운로드)
```

### 5. Unity 열기
- Unity Hub → Open → 클론한 폴더 선택
- Unity가 자동으로 `Library/` 폴더 재생성 (수분 소요)

---

## 실행 방법

### 매 실행 시 순서 (반드시 지킬 것)

**① Python 먼저 실행**
```bash
cd [프로젝트 루트 폴더]
python hand_tracker.py
```
아래 메시지가 뜨면 정상:
```
[Python] HandLandmarker 초기화 완료
[Python] Unity 연결 대기 중... 127.0.0.1:5005
```

**② Unity Play 버튼 클릭**

Console 창에 아래 메시지 확인:
```
[HandReceiver] Sphere 21개 생성 완료
[HandReceiver] Python 연결 성공!
```

**③ 웹캠 앞에 손을 올리면 Unity에서 손 추적 시작**

---

## 씬 구성
```
Hierarchy
├── Main Camera
├── Directional Light
├── Global Volume
└── HandManager          ← HandReceiver + HandSkeleton 스크립트
    ├── Joint_00 (손목)
    ├── Joint_01~04 (엄지)
    ├── Joint_05~08 (검지)
    ├── Joint_09~12 (중지)
    ├── Joint_13~16 (약지)
    └── Joint_17~20 (소지)
```

---

## Inspector 설정값 (HandReceiver)
| 항목 | 기본값 | 설명 |
|------|--------|------|
| Host | 127.0.0.1 | 같은 PC면 그대로 |
| Port | 5005 | Python과 동일해야 함 |
| Sphere Size | 0.05 | 관절 구 크기 |
| Scale XY | 3 | 손 크기 조절 |
| Hand Depth | 3 | 카메라에서 손까지 거리 |
| Smooth Speed | 20 | 높을수록 빠르게 따라옴 |

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| `연결 실패` 에러 | Python보다 Unity를 먼저 실행 | Python 먼저 실행 후 Play |
| 손이 안 보임 | 웹캠 인식 불가 | `cv2.VideoCapture(0)` → `(1)` 로 변경 |
| 손이 반대 방향 | 좌우반전 설정 문제 | `hand_tracker.py`에서 `cv2.flip` 확인 |
| `ModuleNotFoundError` | 라이브러리 미설치 | `pip install mediapipe opencv-python` |
| 모델 파일 없음 | `hand_landmarker.task` 미다운로드 | 위 4번 단계 참고 |

---

## 팀원
| 역할 | 이름 | 담당 |
|------|------|------|
| 팀장 | 최호민 | |
| 팀원 | 김동휘 | |
| 팀원 | 권민규 | |
| 팀원 | 배건우 | Unity / Python |
| 팀원 | 이서우 | |

지도교수: 김선용 교수님 | 산업체 멘토: 한화 비전 연구원 김나연
