import socket
import threading
import cv2
from djitellopy import Tello
import sys


if len(sys.argv) < 2:
    print("No Ip assigned")
    exit()

ip = sys.argv[1]

UNITY_IP = ip
UDP_RECEIVE_PORT = 5005  # Port on Python escolta Unity
UDP_VIDEO_PORT = 5006    # Port on Python envia el vídeo a Unity
print(f"Initialized with ip: {ip} \n Port for control: {UDP_RECEIVE_PORT} \n Port for video: {UDP_VIDEO_PORT}")
# --- CONFIGURACIÓ DE XARXA ---
#UNITY_IP = "192.168.12.222"
#UDP_RECEIVE_PORT = 5005  # Port on Python escolta Unity
#UDP_VIDEO_PORT = 5006    # Port on Python envia el vídeo a Unity

# Inicialització del Tello
tello = Tello()
tello.connect()
tello.streamon()

video = False
# Socket per rebre ordres de Unity
cmd_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
cmd_socket.bind(("0.0.0.0", UDP_RECEIVE_PORT))

print(f"Battery:{tello.get_battery()}%")

# Socket per enviar vídeo a Unity
video_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

def escoltar_unity():
    global video
    global UNITY_IP
    """Fil asíncron per processar ordres de control de Unity sense congelar el vídeo"""
    while True:
        try:
            data, _ = cmd_socket.recvfrom(1024)
            command = data.decode("utf-8").strip()
            print(f"[UNITY] Ordre rebuda: {command}")

            if command.startswith("video"):
                _, ipV = command.split()
                UNITY_IP = ipV
                video = True
                print(f"[UNITY] Configurat per enviar vídeo a: {UNITY_IP} and video: {video}")
                continue  # No és una comanda de control, només una notificació
            
            # Mapeig d'ordres bàsiques al Tello
            if command == "takeoff": tello.takeoff()
            elif command == "land": tello.land()
            elif command.startswith("rc"):
                # Espera un format "rc x y z yaw" (ex: "rc 0 20 0 0")
                _, lr, fb, ud, yaw = command.split()
                tello.send_rc_control(int(lr), int(fb), int(ud), int(yaw))
        except Exception as e:
            print(f"Error en rebre comanda: {e}")

# Executem l'escolta en segon pla
threading.Thread(target=escoltar_unity, daemon=True).start()

print("Servidor Tello-Unity actiu. Esperant ordres...")

# --- BUCLE PRINCIPAL: STREAMING DE VÍDEO MODIFICAT ---
frame_read = tello.get_frame_read()

while True:
    try:
        if video:
            print(f"[VIDEO] Enviant vídeo a {UNITY_IP}:{UDP_VIDEO_PORT}")
            frame = frame_read.frame
            if frame is not None:
                
                # 1. CORRECCIÓ DE COLOR: Convertim de BGR (OpenCV) a RGB (Unity)
                frame_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                
                # 2. MILLORA DE NITIDESA: Pugem a 640x480 fent servir INTER_AREA para evitar que es vegi borrós
                frame_resized = cv2.resize(frame_rgb, (320, 240), interpolation=cv2.INTER_AREA)
                
                # 3. COMPRESSIÓ: Pugem la qualitat al 80% per guanyar definició
                encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 90]
                _, buffer = cv2.imencode('.jpg', frame_resized, encode_param)
                
                data = buffer.tobytes()
                
                # Enviem el paquet si no supera el límit d'UDP
                if len(data) < 65000:

                    #print(data)
                    #print(f"ip: {UNITY_IP}")
                    video_socket.sendto(data, (UNITY_IP, UDP_VIDEO_PORT))
                else:
                    print("[AVÍS] El frame és massa gran per a un sol paquet UDP, disminueix la qualitat.")
                
    except KeyboardInterrupt:
        break

tello.streamoff()
print("Servidor tancat correctament.")