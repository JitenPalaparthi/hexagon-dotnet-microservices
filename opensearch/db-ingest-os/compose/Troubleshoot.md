# Troubleshooting Guide


## Prometheus 

- To see what all metrics are working in prometheus, from a local machine
- It shows all service metrics those are configured
- browse http://localhost:9090/targets 

## db_migrate container 

- The db_migrate container , runs all the scripts form database/migrations directory.
- It checks for the leader/master node and does runs all scripts
- In order to make it to work run the following command
- if you are redeploying on already available database, make sure not to run db_migrate, unless "if not exist" kind of scripts are written.
- Going forward, once the database is baselined, any changes in the table or objects, should only be done using ALTER scripts.Do not change directly Table columns ..

```bash
podman logs  compose-db-migrator-1
``` 

- If the last line of the log is " Migrations complete.", then it is successfully completed

### Exited containers 

- When you type the following command , there should be only one exited container, which is db_migrate one

```bash
 podman ps --filter="status=exited"
 ```

### HAProxy and Patroni troubleshooting 

- Check the master/leader and slave/follower nodes 

```powershell
 podman exec -it  compose-patroni1-1 psql -U postgres -d postgres -c "SELECT pg_is_in_recovery();"
 ``` 

 ```powershell
 podman exec -it  compose-patroni2-1 psql -U postgres -d postgres -c "SELECT pg_is_in_recovery();"
 ``` 

 ```powershell
 podman exec -it  compose-patroni3-1 psql -U postgres -d postgres -c "SELECT pg_is_in_recovery();"
 ``` 

- if the above output for each container is 
    - f → this node is primary (master).
    - t → this node is a standby (replica/follower).
Note: primary and standby changes (is not answered) at any point of time.That is the whole use if etcd gossip stuff.

- Do never directly write to patroni+pg instances. Use only HAProxy based connection string.

## Networking 

- To see which ports are being used 

### Show all TCP/UDP sockets with process info on Linux

```bash
ss -tulwnp
```

### or with netstat (if installed) on Windows

```powershell
netstat -tulnp
```

### excluded port ranges

```bash
cat /proc/sys/net/ipv4/ip_local_port_range
```

```powershell
netsh interface ipv4 show excludedportrange protocol=tcp
```

