# Logs reais da VM

Depois de rodar tudo e fazer os testes, na VM:

```bash
sudo ./scripts/coletar-evidencias.sh ~/logs-da-vm
```

Copie a pasta `~/logs-da-vm` para cá (`scp -r lu2r6be@192.168.64.2:~/logs-da-vm infra/logs/`) e faça o commit.
Ela traz os logs de cada script (`provision-infra.log`, `provision-app.log`, `monitor.log`, `guardiao.log`,
`teste-*.log`), o `journalctl` de cada serviço e o estado do sistema. O arquivo de segredos não é copiado.
