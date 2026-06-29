CREATE TYPE verification_status AS ENUM ('PENDING', 'VERIFIED', 'EXPIRED', 'INVALIDATED');

CREATE TABLE user_email_binding (
    id          serial       NOT NULL,
    username    varchar(128) NOT NULL,
    email       varchar(256) NOT NULL,
    bound_at    timestamptz  NOT NULL DEFAULT now(),
    updated_at  timestamptz  NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT uq_user_email_binding_username UNIQUE (username),
    CONSTRAINT ck_user_email_binding_email CHECK (email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$')
);

CREATE UNIQUE INDEX idx_user_email_binding_username ON user_email_binding(username);
CREATE INDEX idx_user_email_binding_email ON user_email_binding(email);

CREATE TABLE email_verification_code (
    id          serial              NOT NULL,
    username    varchar(128)        NOT NULL,
    code        varchar(6)          NOT NULL,
    status      verification_status NOT NULL DEFAULT 'PENDING',
    sent_at     timestamptz         NOT NULL DEFAULT now(),
    expire_at   timestamptz         NOT NULL,
    verified_at timestamptz,
    fail_count  integer             NOT NULL DEFAULT 0,
    created_at  timestamptz         NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT ck_ev_code_format CHECK (code ~ '^\d{6}$'),
    CONSTRAINT ck_ev_code_fail_count CHECK (fail_count >= 0 AND fail_count <= 5)
);

CREATE INDEX idx_ev_code_username_status ON email_verification_code(username, status);
CREATE INDEX idx_ev_code_expire_at ON email_verification_code(expire_at);

CREATE TABLE auth_exempt_period (
    id           serial       NOT NULL,
    username     varchar(128) NOT NULL,
    device_id    varchar(128) NOT NULL DEFAULT 'default',
    exempt_start timestamptz  NOT NULL,
    exempt_end   timestamptz  NOT NULL,
    created_at   timestamptz  NOT NULL DEFAULT now(),
    updated_at   timestamptz  NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT uq_auth_exempt_username_device UNIQUE (username, device_id)
);

CREATE UNIQUE INDEX idx_auth_exempt_username_device ON auth_exempt_period(username, device_id);
CREATE INDEX idx_auth_exempt_end ON auth_exempt_period(exempt_end);

CREATE TABLE screenshot_record (
    id           serial       NOT NULL,
    file_path    varchar(512) NOT NULL,
    username     varchar(128) NOT NULL,
    capture_time timestamptz  NOT NULL,
    session_id   varchar(128) NOT NULL,
    file_size    bigint       NOT NULL,
    retain_until date         NOT NULL,
    created_at   timestamptz  NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT uq_screenshot_file_path UNIQUE (file_path)
);

CREATE INDEX idx_screenshot_username_capture ON screenshot_record(username, capture_time);
CREATE INDEX idx_screenshot_retain_until ON screenshot_record(retain_until);
CREATE INDEX idx_screenshot_session_id ON screenshot_record(session_id);