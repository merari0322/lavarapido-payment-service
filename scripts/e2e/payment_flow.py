"""Prueba de punta a punta (E2E) de payment-service con TODOS los servicios reales.

Recorre el flujo que hace una persona en la app, pasando por el api-gateway (8080) igual que la
web y el móvil:

    cliente registra vehículo -> reserva (booking-service) -> canjea cupón -> reporta pago
    admin aprueba (se acreditan puntos + notificación) -> admin reembolsa (se revierten puntos)

Requisitos: todo el backend arriba (ver GUIA-EQUIPO.md, sección 4) y los usuarios de prueba
(cliente@gmail.com / admin@gmail.com). Uso:

    python -X utf8 scripts/e2e/payment_flow.py

Deja la base como estaba: borra la promoción de prueba, desactiva la cuenta de prueba si la creó y
cancela la reserva. El vehículo de prueba queda registrado (placa ZZE + 3 números) y se reutiliza.
"""
import json
import random
import string
import sys
import time
import urllib.error
import urllib.request
from datetime import date, timedelta

GATEWAY = "http://localhost:8080/api/v1"
CLIENT = ("cliente@gmail.com", "Cliente123!")
ADMIN = ("admin@gmail.com", "Admin123!")
failures = 0


# ----------------------------------------------------------------------------- utilidades

def call(method, path, token=None, body=None):
    """Hace una petición HTTP al gateway y devuelve (status, json)."""
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(GATEWAY + path, method=method, data=data, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            return response.status, json.loads(response.read() or "null")
    except urllib.error.HTTPError as error:
        return error.code, json.loads(error.read() or "null")


def check(description, actual, expected):
    """Compara lo obtenido con lo esperado e imprime OK / FALLA."""
    global failures
    if actual == expected:
        print(f"  OK    {description} ({actual})")
    else:
        failures += 1
        print(f"  FALLA {description}: obtuve {actual!r}, esperaba {expected!r}")


def step(title):
    print(f"\n== {title}")


def login(credentials):
    status, body = call("POST", "/auth/login", body={"email": credentials[0], "password": credentials[1]})
    if status != 200:
        sys.exit(f"No pude iniciar sesión como {credentials[0]} ({status}): ¿está arriba security-service?")
    return body["accessToken"], body["user"]["id"]


def balance(token):
    return call("GET", "/loyalty/balance", token)[1]["points"]


# ----------------------------------------------------------------------------- flujo

step("1. Iniciar sesión (security-service emite los tokens)")
client_token, client_id = login(CLIENT)
admin_token, _ = login(ADMIN)
print(f"  cliente id={client_id}, admin con token")

step("2. El cliente tiene un vehículo (customer-service)")
vehicles = call("GET", "/vehicles", client_token)[1]
vehicle = next((v for v in vehicles if str(v.get("licensePlate", "")).startswith("ZZE")), None)
if vehicle is None:
    plate = "ZZE" + "".join(random.choices(string.digits, k=3))
    status, vehicle = call("POST", "/vehicles", client_token,
                           {"licensePlate": plate, "vehicleType": "CAR", "brand": "Mazda", "model": "3", "color": "Gris"})
    check("registrar vehículo de prueba -> 201", status, 201)
print(f"  vehículo {vehicle.get('licensePlate')} (id={vehicle['id']})")

step("3. Buscar un horario libre y reservar (booking-service)")
# el servicio que más puntos da, para poder ver la acreditación y la reversión de puntos
service = max((s for s in call("GET", "/catalog/services", client_token)[1] if s["active"]),
              key=lambda s: s.get("loyaltyPoints") or 0)
vehicle_type_id = 1  # CAR
slot = None
for offset in range(1, 15):
    day = (date.today() + timedelta(days=offset)).isoformat()
    _, availability = call("GET", f"/bookings/availability?date={day}&vehicleTypeId={vehicle_type_id}"
                                  f"&serviceIds={service['id']}", client_token)
    free = [s["time"] for s in (availability or {}).get("slots", []) if s["available"]]
    if availability and availability.get("open") and free:
        slot = (day, free[0])
        break
if slot is None:
    sys.exit("No encontré horarios libres en los próximos 14 días.")
status, booking = call("POST", "/bookings", client_token, {"vehicleId": vehicle["id"], "serviceIds": [service["id"]],
                                                           "date": slot[0], "time": slot[1], "notes": "Prueba E2E pagos"})
check("crear reserva -> 201", status, 201)
check("la reserva nace confirmada", booking["status"], "CONFIRMED")
booking_id, total, points = booking["id"], booking["total"], booking["totalLoyaltyPoints"]
print(f"  reserva {booking['code']} el {slot[0]} {slot[1]}: total {total}, da {points} puntos")
if points == 0:
    print("  AVISO: ningún servicio del catálogo da puntos (loyaltyPoints = 0); la acreditación y la")
    print("         reversión de puntos NO se pueden comprobar en esta corrida. Configura puntos en un")
    print("         servicio desde el admin (Catálogo) para probarlas.")

step("4. El admin prepara una cuenta Nequi y una promoción del 10% (payment-service)")
created_account = None
accounts = call("GET", "/payment-accounts", client_token)[1]
if accounts:
    account_id = accounts[0]["id"]
    print(f"  uso la cuenta existente {accounts[0]['methodName']} (id={account_id})")
else:
    # la cuenta de prueba de una corrida anterior quedó inactiva: se reactiva en vez de crear otra
    test_account = {"methodCode": "NEQUI", "accountHolder": "PRUEBA E2E", "accountNumber": "3000000000",
                    "qrImageUrl": "data:image/png;base64,QQ==", "instructions": "Cuenta de prueba", "active": True}
    previous = next((a for a in call("GET", "/admin/payment-accounts", admin_token)[1]
                     if a["accountHolder"] == "PRUEBA E2E"), None)
    if previous:
        status, created_account = call("PUT", f"/admin/payment-accounts/{previous['id']}", admin_token, test_account)
        check("reactivar cuenta de prueba -> 200", status, 200)
    else:
        status, created_account = call("POST", "/admin/payment-accounts", admin_token, test_account)
        check("crear cuenta de prueba -> 201", status, 201)
    account_id = created_account["id"]
code = "E2E" + "".join(random.choices(string.digits, k=6))
status, promotion = call("POST", "/admin/promotions", admin_token, {
    "code": code, "name": "Prueba E2E", "price": 1, "durationMinutes": 60, "featured": False, "benefits": [],
    "validFrom": date.today().isoformat(), "validTo": (date.today() + timedelta(days=30)).isoformat(),
    "discountPercent": 10, "requiredPoints": 0})
check("crear promoción -> 201", status, 201)

step("5. El cliente canjea el cupón en su reserva")
points_before = balance(client_token)
status, redeemed = call("POST", "/loyalty/redeem", client_token, {"bookingId": booking_id, "code": code.lower()})
check("canjear -> 200", status, 200)
discount = round(total * 0.10, 2)
check("descuento = 10% del total", redeemed["discountAmount"], discount)
check("nuevo total", redeemed["newTotal"], total - discount)
status, again = call("POST", "/loyalty/redeem", client_token, {"bookingId": booking_id, "code": code})
check("canjear dos veces -> 400 PROMOTION_ALREADY_REDEEMED", (status, again["code"]), (400, "PROMOTION_ALREADY_REDEEMED"))

step("6. El cliente reporta el pago con su comprobante")
status, payment = call("POST", "/payments", client_token, {
    "bookingId": booking_id, "paymentAccountId": account_id,
    "transactionReference": f"REF-{code}", "receiptImage": "data:image/png;base64,AAA"})
check("reportar -> 201", status, 201)
check("queda en revisión", payment["status"], "IN_REVIEW")
check("monto = total - cupón (lo calcula el servidor)", payment["amount"], total - discount)
check("la reserva viene de booking-service", payment["booking"]["code"], booking["code"])

step("7. El admin aprueba: se acreditan los puntos y se notifica al cliente")
status, approved = call("POST", f"/admin/payments/{payment['id']}/approve", admin_token)
check("aprobar -> 200 APPROVED", (status, approved["status"]), (200, "APPROVED"))
check(f"saldo +{points} puntos", balance(client_token), points_before + points)
status, twice = call("POST", f"/admin/payments/{payment['id']}/approve", admin_token)
check("aprobar dos veces -> 409 PAYMENT_ALREADY_APPROVED", (status, twice["code"]), (409, "PAYMENT_ALREADY_APPROVED"))
# notification-service recibe payment.confirmed por RabbitMQ (puede tardar un momento)
for _ in range(10):
    _, inbox = call("GET", "/notifications", client_token)
    items = inbox if isinstance(inbox, list) else (inbox or {}).get("content", inbox.get("items", []) if inbox else [])
    if any(booking["code"] in json.dumps(n, ensure_ascii=False) or "pago" in json.dumps(n, ensure_ascii=False).lower()
           for n in items[:5]):
        break
    time.sleep(1)
latest = json.dumps(items[0], ensure_ascii=False)[:160] if items else "(bandeja vacía)"
print(f"  última notificación del cliente: {latest}")

step("8. El admin reembolsa: se revierten los puntos")
status, _ = call("POST", f"/admin/payments/{payment['id']}/refund", admin_token)
check("reembolsar -> 204", status, 204)
check("saldo vuelve a como estaba", balance(client_token), points_before)
check("estado final", call("GET", f"/admin/payments/{payment['id']}", admin_token)[1]["status"], "REFUNDED")

step("9. Limpieza")
check("borrar promoción -> 204", call("DELETE", f"/admin/promotions/{promotion['id']}", admin_token)[0], 204)
if created_account:
    body = {k: created_account[k] for k in ("accountHolder", "accountNumber", "qrImageUrl", "instructions")}
    status, _ = call("PUT", f"/admin/payment-accounts/{account_id}", admin_token,
                     {**body, "methodCode": created_account["methodCode"], "active": False})
    check("desactivar cuenta de prueba -> 200", status, 200)
reasons = call("GET", "/bookings/cancellation-reasons", client_token)[1] or []
status, cancelled = call("POST", f"/bookings/{booking_id}/cancel", client_token,
                         {"reasonCode": reasons[0]["code"] if reasons else None})
print(f"  cancelar la reserva de prueba -> {status} {cancelled.get('status') if isinstance(cancelled, dict) else ''}")

print("\nRESULTADO:", "todo OK" if failures == 0 else f"{failures} fallas")
sys.exit(1 if failures else 0)
