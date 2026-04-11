locals {
  postgres_connection_string = format(
    "Host=%s;Port=5432;Database=%s;Username=%s;Password=%s;Ssl Mode=Require;Trust Server Certificate=true",
    azurerm_postgresql_flexible_server.main.fqdn,
    var.postgres_database_name,
    var.postgres_admin_login,
    random_password.postgres.result,
  )

  # StackExchange.Redis-style string; matches prior Cache-for-Redis primary_connection_string shape.
  redis_connection_string = format(
    "%s:%s,password=%s,ssl=True,abortConnect=False",
    azurerm_managed_redis.main.hostname,
    azurerm_managed_redis.main.default_database[0].port,
    azurerm_managed_redis.main.default_database[0].primary_access_key,
  )
}
