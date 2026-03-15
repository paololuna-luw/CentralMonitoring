create or replace function public.handle_auth_user_created()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
    provider_full_name text;
begin
    provider_full_name :=
        coalesce(
            new.raw_user_meta_data ->> 'full_name',
            trim(
                concat(
                    coalesce(new.raw_user_meta_data ->> 'first_name', ''),
                    ' ',
                    coalesce(new.raw_user_meta_data ->> 'last_name', '')
                )
            ),
            new.email
        );

    insert into public.app_users (id, email, full_name, is_active)
    values (
        new.id,
        new.email,
        nullif(provider_full_name, ''),
        true
    )
    on conflict (id) do update
    set
        email = excluded.email,
        full_name = coalesce(excluded.full_name, public.app_users.full_name),
        is_active = true;

    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;

create trigger on_auth_user_created
after insert on auth.users
for each row
execute function public.handle_auth_user_created();

insert into public.app_users (id, email, full_name, is_active)
select
    u.id,
    u.email,
    coalesce(
        u.raw_user_meta_data ->> 'full_name',
        trim(
            concat(
                coalesce(u.raw_user_meta_data ->> 'first_name', ''),
                ' ',
                coalesce(u.raw_user_meta_data ->> 'last_name', '')
            )
        ),
        u.email
    ),
    true
from auth.users u
on conflict (id) do update
set
    email = excluded.email,
    full_name = coalesce(excluded.full_name, public.app_users.full_name),
    is_active = true;
