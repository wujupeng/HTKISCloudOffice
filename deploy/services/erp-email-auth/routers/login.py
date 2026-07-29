import random
from datetime import datetime, timedelta, timezone

import aiosmtplib
from email.mime.text import MIMEText
from fastapi import APIRouter, Request, Response, Depends
from jose import jwt
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from config import JWT_SECRET, JWT_EXPIRE_DAYS, GUACAMOLE_URL, SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASSWORD, SMTP_FROM, SMTP_USE_TLS, ADMIN_USERNAMES
from database import get_db
from models import UserEmailBinding, EmailVerificationCode, VerificationStatus
from guacamole_admin import ensure_user_and_grant
import httpx


router = APIRouter()


def create_jwt_token(username: str) -> str:
    now = datetime.now(tz=timezone.utc)
    payload = {
        "sub": username,
        "iat": int(now.timestamp()),
        "exp": int((now + timedelta(days=JWT_EXPIRE_DAYS)).timestamp()),
        "type": "erp_auth",
    }
    return jwt.encode(payload, JWT_SECRET, algorithm="HS256")


def set_auth_cookie(response: Response, token: str):
    response.set_cookie(
        key="erp_auth_token",
        value=token,
        max_age=JWT_EXPIRE_DAYS * 86400,
        httponly=True,
        secure=True,
        samesite="lax",
        path="/",
    )


def clear_auth_cookie(response: Response):
    response.set_cookie(
        key="erp_auth_token",
        value="",
        max_age=0,
        httponly=True,
        secure=True,
        samesite="lax",
        path="/",
    )


def mask_email(email: str) -> str:
    parts = email.split("@")
    if len(parts) != 2:
        return "***@***"
    name = parts[0]
    if len(name) <= 1:
        masked = "*"
    else:
        masked = name[0] + "***"
    return f"{masked}@{parts[1]}"


async def verify_guacamole_credentials(username: str, password: str) -> bool:
    try:
        async with httpx.AsyncClient() as client:
            resp = await client.post(
                f"{GUACAMOLE_URL}/api/tokens",
                data={"username": username, "password": password},
                timeout=10.0,
            )
            return resp.status_code == 200
    except Exception:
        return False


async def send_verification_email(username: str, email_addr: str, code: str):
    subject = "【ERP 云办公】登录验证码"
    html = f"""
    <html><body>
    <p>尊敬的用户 {username}：</p>
    <p>您正在登录 ERP 云办公平台，验证码为：</p>
    <h2 style="color:#0066cc;letter-spacing:5px;">{code}</h2>
    <p>验证码 5 分钟内有效，请勿泄露给他人。</p>
    <p>如非本人操作，请忽略此邮件。</p>
    <hr><p style="color:#999;font-size:12px;">— ERP 云办公平台</p>
    </body></html>
    """
    msg = MIMEText(html, "html", "utf-8")
    msg["Subject"] = subject
    msg["From"] = SMTP_FROM
    msg["To"] = email_addr

    try:
        await aiosmtplib.send(
            msg,
            hostname=SMTP_HOST,
            port=SMTP_PORT,
            username=SMTP_USER,
            password=SMTP_PASSWORD,
            use_tls=SMTP_USE_TLS,
            timeout=10,
        )
        return True
    except Exception as e:
        print(f"SMTP error: {e}")
        return False


@router.post("/login")
async def login(request: Request, response: Response, db: AsyncSession = Depends(get_db)):
    body = await request.json()
    username = body.get("username", "").strip()
    password = body.get("password", "")

    if not username or not password:
        return {"success": False, "error_code": "INVALID_CREDENTIALS", "message": "用户名或密码错误"}

    if not await verify_guacamole_credentials(username, password):
        return {"success": False, "error_code": "INVALID_CREDENTIALS", "message": "用户名或密码错误"}

    result = await db.execute(select(UserEmailBinding).where(UserEmailBinding.username == username))
    binding = result.scalar_one_or_none()
    if binding is None:
        return {"success": True, "require_bind_email": True, "message": "请绑定邮箱以完成首次登录验证"}

    old_codes = await db.execute(
        select(EmailVerificationCode).where(
            EmailVerificationCode.username == username,
            EmailVerificationCode.status == VerificationStatus.PENDING,
        )
    )
    for old_code in old_codes.scalars().all():
        old_code.status = VerificationStatus.INVALIDATED
    await db.commit()

    code = f"{random.randint(0, 999999):06d}"
    now_naive = datetime.now()
    new_code = EmailVerificationCode(
        username=username,
        code=code,
        status=VerificationStatus.PENDING,
        expire_at=now_naive + timedelta(minutes=5),
    )
    db.add(new_code)
    await db.commit()

    sent = await send_verification_email(username, binding.email, code)
    if not sent:
        return {"success": False, "error_code": "SMTP_ERROR", "message": "验证码发送失败，请稍后重试"}

    return {
        "success": True,
        "require_email_code": True,
        "email_mask": mask_email(binding.email),
        "message": "验证码已发送到绑定邮箱",
    }


@router.post("/bind-email")
async def bind_email(request: Request, db: AsyncSession = Depends(get_db)):
    body = await request.json()
    username = body.get("username", "").strip()
    email = body.get("email", "").strip()

    if not username or not email:
        return {"success": False, "error_code": "INVALID_INPUT", "message": "用户名和邮箱不能为空"}

    if "@" not in email or "." not in email:
        return {"success": False, "error_code": "INVALID_EMAIL", "message": "邮箱格式不正确"}

    result = await db.execute(select(UserEmailBinding).where(UserEmailBinding.email == email))
    existing = result.scalar_one_or_none()
    if existing and existing.username != username:
        return {"success": False, "error_code": "EMAIL_IN_USE", "message": "该邮箱已被其他用户绑定"}

    result = await db.execute(select(UserEmailBinding).where(UserEmailBinding.username == username))
    binding = result.scalar_one_or_none()
    if binding is None:
        binding = UserEmailBinding(username=username, email=email)
        db.add(binding)
    else:
        binding.email = email
    await db.commit()

    old_codes = await db.execute(
        select(EmailVerificationCode).where(
            EmailVerificationCode.username == username,
            EmailVerificationCode.status == VerificationStatus.PENDING,
        )
    )
    for old_code in old_codes.scalars().all():
        old_code.status = VerificationStatus.INVALIDATED
    await db.commit()

    code = f"{random.randint(0, 999999):06d}"
    now_naive = datetime.now()
    new_code = EmailVerificationCode(
        username=username,
        code=code,
        status=VerificationStatus.PENDING,
        expire_at=now_naive + timedelta(minutes=5),
    )
    db.add(new_code)
    await db.commit()

    sent = await send_verification_email(username, email, code)
    if not sent:
        return {"success": False, "error_code": "SMTP_ERROR", "message": "验证码发送失败，请稍后重试"}

    return {
        "success": True,
        "require_email_code": True,
        "email_mask": mask_email(email),
        "message": "验证码已发送到绑定邮箱",
    }


@router.post("/send-code")
async def send_code(request: Request, db: AsyncSession = Depends(get_db)):
    body = await request.json()
    username = body.get("username", "").strip()

    result = await db.execute(
        select(EmailVerificationCode)
        .where(
            EmailVerificationCode.username == username,
            EmailVerificationCode.status == VerificationStatus.PENDING,
        )
        .order_by(EmailVerificationCode.created_at.desc())
    )
    latest = result.scalar_one_or_none()

    if latest:
        elapsed = (datetime.now() - latest.sent_at.replace(tzinfo=None)).total_seconds()
        if elapsed < 60:
            return {
                "success": False,
                "error_code": "RATE_LIMITED",
                "message": "发送过于频繁，请60秒后重试",
                "resend_after": int(60 - elapsed),
            }

    result = await db.execute(select(UserEmailBinding).where(UserEmailBinding.username == username))
    binding = result.scalar_one_or_none()
    if binding is None:
        return {"success": False, "error_code": "EMAIL_NOT_BOUND", "message": "请先绑定邮箱"}

    old_codes = await db.execute(
        select(EmailVerificationCode).where(
            EmailVerificationCode.username == username,
            EmailVerificationCode.status == VerificationStatus.PENDING,
        )
    )
    for old_code in old_codes.scalars().all():
        old_code.status = VerificationStatus.INVALIDATED
    await db.commit()

    code = f"{random.randint(0, 999999):06d}"
    now_naive = datetime.now()
    new_code = EmailVerificationCode(
        username=username,
        code=code,
        status=VerificationStatus.PENDING,
        expire_at=now_naive + timedelta(minutes=5),
    )
    db.add(new_code)
    await db.commit()

    sent = await send_verification_email(username, binding.email, code)
    if not sent:
        return {"success": False, "error_code": "SMTP_ERROR", "message": "验证码发送失败"}

    return {"success": True, "message": "验证码已发送", "resend_after": 60}


@router.post("/verify-code")
async def verify_code(request: Request, response: Response, db: AsyncSession = Depends(get_db)):
    body = await request.json()
    username = body.get("username", "").strip()
    code = body.get("code", "").strip()

    if not username or not code:
        return {"success": False, "error_code": "INVALID_CODE", "message": "验证码错误"}

    result = await db.execute(
        select(EmailVerificationCode)
        .where(
            EmailVerificationCode.username == username,
            EmailVerificationCode.status == VerificationStatus.PENDING,
        )
        .order_by(EmailVerificationCode.created_at.desc())
    )
    record = result.scalar_one_or_none()

    if record is None:
        return {"success": False, "error_code": "CODE_INVALIDATED", "message": "验证码已失效，请重新获取"}

    now_naive = datetime.now()
    if now_naive > record.expire_at.replace(tzinfo=None):
        record.status = VerificationStatus.EXPIRED
        await db.commit()
        return {"success": False, "error_code": "CODE_EXPIRED", "message": "验证码已过期，请重新获取"}

    if record.code != code:
        record.fail_count += 1
        if record.fail_count >= 5:
            record.status = VerificationStatus.INVALIDATED
            await db.commit()
            return {"success": False, "error_code": "CODE_INVALIDATED", "message": "验证码已失效，请重新获取"}
        await db.commit()
        return {
            "success": False,
            "error_code": "INVALID_CODE",
            "message": "验证码错误",
            "attempts_remaining": 5 - record.fail_count,
        }

    record.status = VerificationStatus.VERIFIED
    record.verified_at = now_naive
    await db.commit()

    try:
        await ensure_user_and_grant(username)
    except Exception as e:
        print(f"Failed to grant Guacamole permissions for {username}: {e}")

    token = create_jwt_token(username)
    set_auth_cookie(response, token)
    return {"success": True, "redirect": "/guacamole/"}


@router.post("/logout")
async def logout(response: Response):
    clear_auth_cookie(response)
    return {"success": True, "redirect": "/api/erp-auth/login-page"}


@router.get("/login-page")
async def login_page():
    from fastapi.responses import HTMLResponse
    from jinja2 import Environment, FileSystemLoader
    import os

    templates_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "templates")
    env = Environment(loader=FileSystemLoader(templates_dir))
    template = env.get_template("login.html")
    return HTMLResponse(content=template.render())