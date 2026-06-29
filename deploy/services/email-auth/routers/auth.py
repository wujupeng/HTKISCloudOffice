from fastapi import APIRouter, Request, Response
from jose import jwt, JWTError
from config import JWT_SECRET
from datetime import datetime, timezone

router = APIRouter()


def verify_jwt_from_cookie(request: Request) -> dict | None:
    token = request.cookies.get("htkis_auth_token")
    if not token:
        return None
    try:
        payload = jwt.decode(token, JWT_SECRET, algorithms=["HS256"])
        if payload.get("type") != "htkis_auth":
            return None
        return payload
    except JWTError:
        return None


@router.get("/auth")
async def auth_check(request: Request):
    payload = verify_jwt_from_cookie(request)
    if payload is None:
        return Response(status_code=401)
    exp = payload.get("exp")
    if exp and datetime.fromtimestamp(exp, tz=timezone.utc) < datetime.now(tz=timezone.utc):
        return Response(status_code=401)
    return Response(status_code=200)