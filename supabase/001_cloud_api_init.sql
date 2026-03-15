create extension if not exists "pgcrypto";

create table if not exists organizations (
    id uuid primary key default gen_random_uuid(),
    name text not null,
    slug text not null unique,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default timezone('utc', now())
);

create table if not exists app_users (
    id uuid primary key,
    email text not null unique,
    full_name text,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default timezone('utc', now())
);

create table if not exists organization_users (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references organizations(id) on delete cascade,
    user_id uuid not null references app_users(id) on delete cascade,
    role text not null,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default timezone('utc', now()),
    constraint uq_organization_users unique (organization_id, user_id),
    constraint ck_organization_users_role check (role in ('owner', 'admin', 'operator', 'readonly'))
);

create table if not exists central_instances (
    id uuid primary key default gen_random_uuid(),
    organization_id uuid not null references organizations(id) on delete cascade,
    instance_id uuid not null unique,
    instance_name text not null,
    description text,
    api_key_hash text,
    last_seen_utc timestamptz,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default timezone('utc', now())
);

create index if not exists ix_central_instances_organization_id
    on central_instances (organization_id);

create table if not exists central_user_access (
    id uuid primary key default gen_random_uuid(),
    central_instance_id uuid not null references central_instances(id) on delete cascade,
    user_id uuid not null references app_users(id) on delete cascade,
    role text not null,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default timezone('utc', now()),
    constraint uq_central_user_access unique (central_instance_id, user_id),
    constraint ck_central_user_access_role check (role in ('owner', 'admin', 'operator', 'readonly'))
);

create index if not exists ix_central_user_access_user_id
    on central_user_access (user_id);

create table if not exists central_snapshots (
    id uuid primary key default gen_random_uuid(),
    central_instance_id uuid not null references central_instances(id) on delete cascade,
    snapshot_timestamp_utc timestamptz not null,
    hosts_total integer not null default 0,
    hosts_active integer not null default 0,
    alerts_open integer not null default 0,
    critical_alerts integer not null default 0,
    warning_alerts integer not null default 0,
    summary_json jsonb,
    created_at_utc timestamptz not null default timezone('utc', now())
);

create index if not exists ix_central_snapshots_instance_timestamp
    on central_snapshots (central_instance_id, snapshot_timestamp_utc desc);

create table if not exists cloud_alerts (
    id uuid primary key default gen_random_uuid(),
    central_instance_id uuid not null references central_instances(id) on delete cascade,
    source_alert_id uuid not null,
    host_id uuid,
    host_name text,
    metric_key text not null,
    severity text not null,
    status text not null,
    trigger_value double precision,
    threshold double precision,
    labels_json jsonb,
    opened_at_utc timestamptz not null,
    resolved_at_utc timestamptz,
    last_synced_at_utc timestamptz not null default timezone('utc', now()),
    created_at_utc timestamptz not null default timezone('utc', now()),
    constraint uq_cloud_alerts_source unique (central_instance_id, source_alert_id),
    constraint ck_cloud_alerts_severity check (severity in ('Critical', 'Warning', 'Info')),
    constraint ck_cloud_alerts_status check (status in ('Open', 'Acked', 'Resolved'))
);

create index if not exists ix_cloud_alerts_instance_status
    on cloud_alerts (central_instance_id, status, opened_at_utc desc);

create index if not exists ix_cloud_alerts_metric_key
    on cloud_alerts (metric_key);

create table if not exists mobile_device_tokens (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references app_users(id) on delete cascade,
    platform text not null,
    device_token text not null unique,
    is_active boolean not null default true,
    created_at_utc timestamptz not null default timezone('utc', now()),
    last_seen_utc timestamptz not null default timezone('utc', now()),
    constraint ck_mobile_device_tokens_platform check (platform in ('android', 'ios'))
);

create index if not exists ix_mobile_device_tokens_user_id
    on mobile_device_tokens (user_id);

create or replace view vw_user_centrals as
select
    au.id as user_id,
    au.email,
    ci.id as central_id,
    ci.instance_id,
    ci.instance_name,
    ci.organization_id,
    cua.role,
    ci.last_seen_utc,
    ci.is_active
from app_users au
join central_user_access cua on cua.user_id = au.id and cua.is_active = true
join central_instances ci on ci.id = cua.central_instance_id and ci.is_active = true;
