from fastapi import APIRouter, Request
from fastapi.responses import HTMLResponse, RedirectResponse
from jinja2 import Environment, FileSystemLoader
from jose import jwt, JWTError
import os

from config import JWT_SECRET, ADMIN_USERNAMES

router = APIRouter()


def check_admin(request: Request) -> str | None:
    token = request.cookies.get("htkis_auth_token")
    if not token:
        return None
    try:
        payload = jwt.decode(token, JWT_SECRET, algorithms=["HS256"])
        if payload.get("type") != "htkis_auth":
            return None
        username = payload.get("sub", "")
        if username not in ADMIN_USERNAMES:
            return None
        return username
    except JWTError:
        return None


@router.get("/")
async def index(request: Request):
    if check_admin(request) is None:
        return RedirectResponse(url="/api/email-auth/login-page")

    templates_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "templates")
    env = Environment(loader=FileSystemLoader(templates_dir))
    template = env.get_template("screenshot.html")
    return HTMLResponse(content=template.render())