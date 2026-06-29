from sqlalchemy import Column, Integer, String, DateTime, Enum, CheckConstraint, UniqueConstraint, BigInteger, Date
from sqlalchemy.sql import func
from database import Base
import enum


class VerificationStatus(str, enum.Enum):
    PENDING = "PENDING"
    VERIFIED = "VERIFIED"
    EXPIRED = "EXPIRED"
    INVALIDATED = "INVALIDATED"


class UserEmailBinding(Base):
    __tablename__ = "user_email_binding"

    id = Column(Integer, primary_key=True, autoincrement=True)
    username = Column(String(128), nullable=False, unique=True)
    email = Column(String(256), nullable=False)
    bound_at = Column(DateTime(timezone=True), nullable=False, server_default=func.now())
    updated_at = Column(DateTime(timezone=True), nullable=False, server_default=func.now(), onupdate=func.now())


class EmailVerificationCode(Base):
    __tablename__ = "email_verification_code"

    id = Column(Integer, primary_key=True, autoincrement=True)
    username = Column(String(128), nullable=False)
    code = Column(String(6), nullable=False)
    status = Column(Enum(VerificationStatus, name="verification_status"), nullable=False, default=VerificationStatus.PENDING)
    sent_at = Column(DateTime(timezone=True), nullable=False, server_default=func.now())
    expire_at = Column(DateTime(timezone=True), nullable=False)
    verified_at = Column(DateTime(timezone=True), nullable=True)
    fail_count = Column(Integer, nullable=False, default=0)
    created_at = Column(DateTime(timezone=True), nullable=False, server_default=func.now())


class AuthExemptPeriod(Base):
    __tablename__ = "auth_exempt_period"

    id = Column(Integer, primary_key=True, autoincrement=True)
    username = Column(String(128), nullable=False)
    device_id = Column(String(128), nullable=False, default="default")
    exempt_start = Column(DateTime(timezone=True), nullable=False)
    exempt_end = Column(DateTime(timezone=True), nullable=False)
    created_at = Column(DateTime(timezone=True), nullable=False, server_default=func.now())
    updated_at = Column(DateTime(timezone=True), nullable=False, server_default=func.now(), onupdate=func.now())

    __table_args__ = (
        UniqueConstraint("username", "device_id", name="uq_auth_exempt_username_device"),
    )


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