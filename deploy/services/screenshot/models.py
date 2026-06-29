from sqlalchemy import Column, Integer, String, DateTime, BigInteger, Date, UniqueConstraint
from sqlalchemy.sql import func
from database import Base


class ScreenshotRecord(Base):
    __tablename__ = "screenshot_record"

    id = Column(Integer, primary_key=True, autoincrement=True)
    file_path = Column(String(512), nullable=False, unique=True)
    username = Column(String(128), nullable=False)
    capture_time = Column(DateTime(timezone=True), nullable=False)
    session_id = Column(String(128), nullable=False)
    file_size = Column(BigInteger, nullable=False)
    retain_until = Column(Date, nullable=False)
    created_at = Column(DateTime(timezone=True), nullable=False, server_default=func.now())