"""Contrats JSON de l'API (en camelCase, comme attendu par le BFF et le front)."""
import uuid
from datetime import datetime

from pydantic import BaseModel, ConfigDict, EmailStr, Field
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class RegisterRequest(CamelModel):
    email: EmailStr
    password: str = Field(max_length=128)
    display_name: str = Field(min_length=1, max_length=60)


class LoginRequest(CamelModel):
    email: str
    password: str


class RefreshRequest(CamelModel):
    refresh_token: str


class UserOut(CamelModel):
    id: uuid.UUID
    email: str
    display_name: str
    roles: list[str]


class AuthResponse(CamelModel):
    access_token: str
    access_token_expires_at_utc: datetime
    refresh_token: str
    refresh_token_expires_at_utc: datetime
    user: UserOut
