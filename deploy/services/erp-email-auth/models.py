from sqlalchemy import Column, Integer, String, DateTime, Enum, UniqueConstraint
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