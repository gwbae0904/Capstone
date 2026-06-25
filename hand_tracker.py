"""
hand_tracker.py  (바이너리 전송 버전)
JSON 대신 struct.pack으로 21개 좌표를 252바이트로 압축 전송
"""

import cv2
import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision as mp_vision
import socket
import struct
import time
import os
import urllib.request

HAND_CONNECTIONS = [
    (0,1),(1,2),(2,3),(3,4),
    (0,5),(5,6),(6,7),(7,8),
    (0,9),(9,10),(10,11),(11,12),
    (0,13),(13,14),(14,15),(15,16),
    (0,17),(17,18),(18,19),(19,20),
    (5,9),(9,13),(13,17),(0,17),
]
FINGER_COLORS = [(0,0,255),(0,255,0),(255,165,0),(255,0,255),(0,255,255)]
LANDMARK_FINGER = [0, 0,0,0,0, 1,1,1,1, 2,2,2,2, 3,3,3,3, 4,4,4,4]

# 패킷 포맷: 매직넘버(2B) + 랜드마크 21개 × xyz(4B×3) = 2 + 252 = 254 bytes
MAGIC   = b'\xAB\xCD'        # 패킷 시작 식별자
FMT     = '>' + '3f' * 21   # big-endian, 21개 × (x,y,z) float

MODEL_PATH = "hand_landmarker.task"
if not os.path.exists(MODEL_PATH):
    url = ("https://storage.googleapis.com/mediapipe-models/"
           "hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task")
    print("[Python] 모델 다운로드 중...")
    urllib.request.urlretrieve(url, MODEL_PATH)

options = mp_vision.HandLandmarkerOptions(
    base_options=mp_python.BaseOptions(model_asset_path=MODEL_PATH),
    running_mode=mp_vision.RunningMode.VIDEO,
    num_hands=1,
    min_hand_detection_confidence=0.7,
    min_hand_presence_confidence=0.6,
    min_tracking_confidence=0.6,
)
landmarker = mp_vision.HandLandmarker.create_from_options(options)
print("[Python] 초기화 완료")

HOST, PORT = "127.0.0.1", 5005
server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server_sock.bind((HOST, PORT))
server_sock.listen(1)
print(f"[Python] Unity 연결 대기 중... {HOST}:{PORT}")
conn, addr = server_sock.accept()
conn.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)  # Nagle 알고리즘 비활성화
print(f"[Python] Unity 연결됨: {addr}")

cap = cv2.VideoCapture(0)
timestamp_ms = 0

try:
    while cap.isOpened():
        ret, frame = cap.read()
        if not ret:
            break

        frame = cv2.flip(frame, 1)
        rgb   = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

        timestamp_ms += 33
        mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        result   = landmarker.detect_for_video(mp_image, timestamp_ms)

        if result.hand_landmarks:
            landmarks = result.hand_landmarks[0]
            h, w = frame.shape[:2]
            pts = [(int(lm.x * w), int(lm.y * h)) for lm in landmarks]

            # 시각화
            for (a, b) in HAND_CONNECTIONS:
                cv2.line(frame, pts[a], pts[b], (200,200,200), 2)
            for i, (cx, cy) in enumerate(pts):
                col = FINGER_COLORS[LANDMARK_FINGER[i]]
                cv2.circle(frame, (cx, cy), 8, col, -1)
                cv2.circle(frame, (cx, cy), 8, (255,255,255), 1)

            # 바이너리 패킷 전송 (254 bytes)
            coords = []
            for lm in landmarks:
                coords += [lm.x, lm.y, lm.z]
            packet = MAGIC + struct.pack(FMT, *coords)
            try:
                conn.sendall(packet)
            except (BrokenPipeError, ConnectionResetError):
                print("[Python] Unity 연결 끊김")
                break

        cv2.imshow("Hand Tracker (q: quit)", frame)
        if cv2.waitKey(1) & 0xFF == ord("q"):
            break

finally:
    cap.release()
    cv2.destroyAllWindows()
    conn.close()
    server_sock.close()
    landmarker.close()
    print("[Python] 종료")
