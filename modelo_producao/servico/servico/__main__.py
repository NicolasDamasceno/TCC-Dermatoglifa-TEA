"""`python -m servico`: sobe o serviço só na interface local (rede interna, ADR-0001)."""

import logging
import os

import uvicorn


def principal() -> None:
    logging.basicConfig(level=logging.INFO, format='%(asctime)s %(levelname)s %(name)s: %(message)s')
    uvicorn.run(
        'servico.app:app',
        host=os.environ.get('DERMATOGLIFIA_HOST', '127.0.0.1'),
        port=int(os.environ.get('DERMATOGLIFIA_PORTA', '8000')),
        workers=1,
        access_log=False,  # o caminho já basta no log do back; nada de corpo de requisição
    )


if __name__ == '__main__':
    principal()
