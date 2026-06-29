import re
from fastapi import APIRouter, Request, Depends
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from config import ADMIN_USERNAMES
from database import get_db
from models import UserEmailBinding
from routers.auth import verify_jwt_from_cookie

router = APIRouter()


def check_admin(request: Request) -> str | None:
    payload = verify_jwt_from_cookie(request)
    if payload is None:
        return None
    username = payload.get("sub", "")
    if username not in ADMIN_USERNAMES:
        return None
    return username


@router.post("/bind-email")
async def bind_email(request: Request, db: AsyncSession = Depends(get_db)):
    admin = check_admin(request)
    if admin is None:
        return {"success": False, "error_code": "FORBIDDEN", "message": "权限不足"}

    body = await request.json()
    username = body.get("username", "").strip()
    email = body.get("email", "").strip()

    if not username or not email:
        return {"success": False, "error_code": "INVALID_PARAMS", "message": "参数不完整"}

    email_pattern = r"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$"
    if not re.match(email_pattern, email, re.IGNORECASE):
        return {"success": False, "error_code": "INVALID_EMAIL", "message": "邮箱格式不合法"}

    result = await db.execute(select(UserEmailBinding).where(UserEmailBinding.username == username))
    binding = result.scalar_one_or_none()

    if binding:
        binding.email = email
        binding.updated_at = __import__("datetime").datetime.now(__import__("datetime").timezone.utc)
    else:
        binding = UserEmailBinding(username=username, email=email)
        db.add(binding)

    await db.commit()
    return {"success": True, "message": "邮箱绑定成功"}


@router.get("/emails")
async def list_emails(request: Request, db: AsyncSession = Depends(get_db)):
    admin = check_admin(request)
    if admin is None:
        return {"success": False, "error_code": "FORBIDDEN", "message": "权限不足"}

    result = await db.execute(select(UserEmailBinding).order_by(UserEmailBinding.username))
    bindings = result.scalars().all()

    data = []
    for b in bindings:
        data.append({
            "username": b.username,
            "email": b.email,
            "bound_at": b.bound_at.isoformat() if b.bound_at else None,
            "updated_at": b.updated_at.isoformat() if b.updated_at else None,
        })

    return {"success": True, "data": data}


@router.delete("/bind-email")
async def unbind_email(request: Request, db: AsyncSession = Depends(get_db)):
    admin = check_admin(request)
    if admin is None:
        return {"success": False, "error_code": "FORBIDDEN", "message": "权限不足"}

    body = await request.json()
    username = body.get("username", "").strip()

    if not username:
        return {"success": False, "error_code": "INVALID_PARAMS", "message": "参数不完整"}

    result = await db.execute(select(UserEmailBinding).where(UserEmailBinding.username == username))
    binding = result.scalar_one_or_none()

    if binding:
        await db.delete(binding)
        await db.commit()

    return {"success": True, "message": "邮箱绑定已解除"}


@router.get("/emails-page")
async def emails_page(request: Request):
    admin = check_admin(request)
    if admin is None:
        from fastapi.responses import RedirectResponse
        return RedirectResponse(url="/api/email-auth/login-page")

    from fastapi.responses import HTMLResponse
    from jinja2 import Environment, FileSystemLoader
    import os

    templates_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "templates")
    env = Environment(loader=FileSystemLoader(templates_dir))
    template = env.get_template("admin_emails.html")
    return HTMLResponse(content=template.render())