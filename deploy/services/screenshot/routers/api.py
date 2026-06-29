import os
import re
from datetime import datetime, timedelta, timezone
from pathlib import Path

from fastapi import APIRouter, Request, Query, Depends
from fastapi.responses import FileResponse, JSONResponse
from jose import jwt, JWTError
from sqlalchemy import select, func, and_
from sqlalchemy.ext.asyncio import AsyncSession

from config import SCREENSHOT_DIR, JWT_SECRET, ADMIN_USERNAMES
from database import get_db
from models import ScreenshotRecord

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


@router.get("/list")
async def list_screenshots(
    request: Request,
    username: str = Query(...),
    date_from: str = Query(...),
    date_to: str = Query(...),
    page: int = Query(1, ge=1),
    page_size: int = Query(100, ge=1, le=500),
    db: AsyncSession = Depends(get_db),
):
    if check_admin(request) is None:
        return JSONResponse(status_code=403, content={"success": False, "message": "权限不足"})

    try:
        dt_from = datetime.strptime(date_from, "%Y-%m-%d").replace(tzinfo=timezone.utc)
        dt_to = datetime.strptime(date_to, "%Y-%m-%d").replace(hour=23, minute=59, second=59, tzinfo=timezone.utc)
    except ValueError:
        return {"success": False, "message": "日期格式错误"}

    count_q = select(func.count()).select_from(ScreenshotRecord).where(
        and_(
            ScreenshotRecord.username == username,
            ScreenshotRecord.capture_time >= dt_from,
            ScreenshotRecord.capture_time <= dt_to,
        )
    )
    total = (await db.execute(count_q)).scalar() or 0

    q = select(ScreenshotRecord).where(
        and_(
            ScreenshotRecord.username == username,
            ScreenshotRecord.capture_time >= dt_from,
            ScreenshotRecord.capture_time <= dt_to,
        )
    ).order_by(ScreenshotRecord.capture_time.desc()).offset((page - 1) * page_size).limit(page_size)

    records = (await db.execute(q)).scalars().all()

    return {
        "success": True,
        "data": {
            "total": total,
            "page": page,
            "page_size": page_size,
            "records": [
                {
                    "file_path": r.file_path,
                    "username": r.username,
                    "capture_time": r.capture_time.isoformat() if r.capture_time else None,
                    "file_size": r.file_size,
                    "session_id": r.session_id,
                }
                for r in records
            ],
        },
    }


@router.get("/image")
async def get_image(request: Request, path: str = Query(...)):
    if check_admin(request) is None:
        return JSONResponse(status_code=403, content={"success": False, "message": "权限不足"})

    if ".." in path or path.startswith("/"):
        return {"success": False, "error_code": "INVALID_PATH", "message": "非法路径"}

    full_path = os.path.join(SCREENSHOT_DIR, path)
    if not os.path.isfile(full_path):
        return {"success": False, "error_code": "FILE_NOT_FOUND", "message": "截屏文件不存在"}

    return FileResponse(full_path, media_type="image/png")


@router.get("/stats")
async def get_stats(request: Request, db: AsyncSession = Depends(get_db)):
    if check_admin(request) is None:
        return JSONResponse(status_code=403, content={"success": False, "message": "权限不足"})

    total_files = (await db.execute(select(func.count()).select_from(ScreenshotRecord))).scalar() or 0
    total_size = (await db.execute(select(func.sum(ScreenshotRecord.file_size)))).scalar() or 0
    oldest = (await db.execute(select(func.min(ScreenshotRecord.capture_time)))).scalar()
    newest = (await db.execute(select(func.max(ScreenshotRecord.capture_time)))).scalar()

    users_q = select(
        ScreenshotRecord.username,
        func.count().label("file_count"),
        func.sum(ScreenshotRecord.file_size).label("size_bytes"),
    ).group_by(ScreenshotRecord.username)
    users = (await db.execute(users_q)).all()

    disk_usage = 0
    try:
        stat = os.statvfs(SCREENSHOT_DIR)
        disk_usage = round((1 - stat.f_bavail / stat.f_blocks) * 100, 1) if stat.f_blocks > 0 else 0
    except Exception:
        pass

    return {
        "success": True,
        "data": {
            "total_files": total_files,
            "total_size_mb": round(total_size / 1024 / 1024, 1) if total_size else 0,
            "oldest_date": oldest.strftime("%Y-%m-%d") if oldest else None,
            "newest_date": newest.strftime("%Y-%m-%d") if newest else None,
            "users": [
                {"username": u.username, "file_count": u.file_count, "size_mb": round(u.size_bytes / 1024 / 1024, 1) if u.size_bytes else 0}
                for u in users
            ],
            "disk_usage_percent": disk_usage,
        },
    }


@router.post("/scan")
async def scan_screenshots(request: Request, db: AsyncSession = Depends(get_db)):
    if check_admin(request) is None:
        return JSONResponse(status_code=403, content={"success": False, "message": "权限不足"})

    scanned = 0
    skipped = 0
    base = Path(SCREENSHOT_DIR)

    if not base.exists():
        return {"success": True, "scanned": 0, "skipped": 0, "message": "目录不存在"}

    for user_dir in base.iterdir():
        if not user_dir.is_dir():
            continue
        username = user_dir.name
        for date_dir in user_dir.iterdir():
            if not date_dir.is_dir():
                continue
            date_str = date_dir.name
            for img_file in date_dir.glob("*.png"):
                rel_path = f"{username}/{date_str}/{img_file.name}"
                exists = (await db.execute(select(ScreenshotRecord).where(ScreenshotRecord.file_path == rel_path))).scalar_one_or_none()
                if exists:
                    skipped += 1
                    continue

                try:
                    time_str = img_file.stem
                    capture_dt = datetime.strptime(f"{date_str} {time_str}", "%Y-%m-%d %H%M%S").replace(tzinfo=timezone.utc)
                except ValueError:
                    skipped += 1
                    continue

                record = ScreenshotRecord(
                    file_path=rel_path,
                    username=username,
                    capture_time=capture_dt,
                    session_id=f"rdp-{username}-{date_str}",
                    file_size=img_file.stat().st_size,
                    retain_until=(capture_dt + timedelta(days=90)).date(),
                )
                db.add(record)
                scanned += 1

    await db.commit()
    return {"success": True, "scanned": scanned, "skipped": skipped}