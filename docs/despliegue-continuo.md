# Despliegue continuo

Cuando un cambio entra a `main` (PR aprobado) y el CI pasa, el job **Pedir despliegue** de
[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) lanza por la API de GitHub el workflow
**Desplegar en Azure** de [lavarapido-infra](https://github.com/BrayanCABRERA123/lavarapido-infra)
(`automatic=true`, `source=<repo>@<commit>`). Ese workflow baja `main` de todos los repos en el
servidor y reconstruye solo lo que cambió; si la VM está apagada, deja un aviso y el servidor se
pone al día solo al encenderse. En `develop` y en los pull request el job no corre.

## Secreto que hace falta

| Secreto | Qué es |
|---|---|
| `DEPLOY_DISPATCH_TOKEN` | Token *fine-grained* de GitHub con acceso **solo** a `BrayanCABRERA123/lavarapido-infra` y permiso **Actions: Read and write**. Solo sirve para lanzar el despliegue |

- **El token** lo genera Brayan, dueño de `lavarapido-infra` (GitHub → Settings → Developer
  settings → Fine-grained tokens): un token *fine-grained* solo da acceso a repos de quien lo crea.
- **El secreto** lo crea en este repo su dueño (`merari0322`) o alguien con rol **Admin**:
  Settings → Secrets and variables → Actions → New repository secret. Con permiso de escritura
  (push) no alcanza.
- Si falta o venció, el job sale en rojo ("Falta el secreto DEPLOY_DISPATCH_TOKEN" o el error
  401/403 de la API) y no se despliega; el cambio llega con el siguiente despliegue de cualquier
  repo o a mano (`DEPLOY.md` de lavarapido-infra).
