from __future__ import annotations

import socket

import httpx2
import pytest
from factories import NetworkBlockedError

REMOTE_HOST = "remote.example.test"
LOOPBACK_PROXY = "http://127.0.0.1:9"
NON_ROUTABLE_ADDRESS = "10.255.255.1"
HTTPS_PORT = 443
CONNECT_TIMEOUT_SECONDS = 0.1


def test_should_block_name_lookup_of_remote_host() -> None:
    with pytest.raises(NetworkBlockedError, match=REMOTE_HOST):
        socket.create_connection((REMOTE_HOST, HTTPS_PORT), CONNECT_TIMEOUT_SECONDS)


def test_should_block_connect_to_remote_address() -> None:
    with socket.socket() as sock:
        sock.settimeout(CONNECT_TIMEOUT_SECONDS)

        with pytest.raises(NetworkBlockedError, match=NON_ROUTABLE_ADDRESS):
            sock.connect((NON_ROUTABLE_ADDRESS, HTTPS_PORT))


def test_should_block_connect_ex_to_remote_address() -> None:
    with socket.socket() as sock:
        sock.settimeout(CONNECT_TIMEOUT_SECONDS)

        with pytest.raises(NetworkBlockedError, match=NON_ROUTABLE_ADDRESS):
            sock.connect_ex((NON_ROUTABLE_ADDRESS, HTTPS_PORT))


def test_should_allow_connection_to_loopback_server() -> None:
    with socket.create_server(("127.0.0.1", 0)) as server:
        port = server.getsockname()[1]

        with socket.create_connection(("localhost", port)) as client:
            peer = client.getpeername()

    assert peer[1] == port


def test_should_block_remote_request_when_proxy_env_is_set(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("HTTPS_PROXY", LOOPBACK_PROXY)

    with (
        httpx2.Client() as client,
        pytest.raises(NetworkBlockedError, match=REMOTE_HOST),
    ):
        client.get(f"https://{REMOTE_HOST}/")
