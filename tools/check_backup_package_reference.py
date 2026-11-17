#!/usr/bin/env python3
"""Independent Python format model + pinned C# reader vector. NOT C#/.NET/DPAPI execution.
Run in a throw-away Python env with cryptography installed. All bytes/passwords are synthetic.
"""
from __future__ import annotations
import hashlib
from pathlib import Path
import re
import struct
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

ROOT = Path(__file__).resolve().parents[1]
PASSWORD = b"synthetic-test-backup-only-2026"
CHUNK = 1024 * 1024
MAX = 32 * 1024**3


def encode(payload: bytes) -> bytes:
    # Deterministic public test parameters ONLY. Production MUST randomize both values.
    header = b"A11BAK01" + bytes(range(16)) + bytes(range(16, 24)) + struct.pack("<q", len(payload))
    key = hashlib.pbkdf2_hmac("sha256", PASSWORD, header[8:24], 600000, 32)
    aes = AESGCM(key)
    records = []
    for index, start in enumerate(range(0, len(payload), CHUNK)):
        counter = struct.pack("<I", index)
        records.append(aes.encrypt(header[24:32] + counter, payload[start:start + CHUNK], header + counter))
    counter = struct.pack("<I", len(records))
    records.append(aes.encrypt(header[24:32] + counter, hashlib.sha256(payload).digest(), header + counter))
    return header + b"".join(records)


def decode(package: bytes, password: bytes = PASSWORD) -> bytes:
    if len(package) < 40 or package[:8] != b"A11BAK01":
        raise ValueError("header")
    header = package[:40]
    size, = struct.unpack("<q", header[32:40])
    if not 0 <= size <= MAX:
        raise ValueError("bound")
    chunks = (size + CHUNK - 1) // CHUNK
    if len(package) != 40 + size + chunks * 16 + 48:
        raise ValueError("length")
    key = hashlib.pbkdf2_hmac("sha256", password, header[8:24], 600000, 32)
    aes = AESGCM(key)
    cursor, remaining, output = 40, size, bytearray()
    for index in range(chunks):
        take = min(remaining, CHUNK)
        counter = struct.pack("<I", index)
        output.extend(aes.decrypt(header[24:32] + counter, package[cursor:cursor + take + 16], header + counter))
        cursor += take + 16
        remaining -= take
    counter = struct.pack("<I", chunks)
    digest = aes.decrypt(header[24:32] + counter, package[cursor:], header + counter)
    if digest != hashlib.sha256(output).digest():
        raise ValueError("digest")
    return bytes(output)


def main() -> None:
    vector = encode(b"independent synthetic backup vector")
    tests = (ROOT / "dotnet/Area11.Tests/BackupPackageTests.cs").read_text()
    pinned = re.search(r'ReferenceVector = "([A-Fa-f0-9]+)"', tests)
    assert pinned and bytes.fromhex(pinned[1]) == vector, "C# reader vector drift"
    for size in (0, 1, CHUNK - 1, CHUNK, CHUNK + 1, CHUNK * 2 + 23):
        payload = bytes((i * 17) % 251 for i in range(size))
        assert decode(encode(payload)) == payload
    negatives = []
    for offset in (0, 7, 8, 23, 24, 31, 32, 39, 40, 74, 90, len(vector) - 1):
        changed = bytearray(vector)
        changed[offset] ^= 1
        negatives.append(bytes(changed))
    negatives.extend(vector[:cut] for cut in (0, 1, 39, 40, len(vector) - 1))
    negatives.extend((vector + b"x", vector + b"\0" * 16))
    for size in (-1, MAX + 1, 2**63 - 1):
        negatives.append(vector[:32] + struct.pack("<q", size) + vector[40:])
    multi = encode(b"A" * CHUNK + b"B" * CHUNK)
    record = CHUNK + 16
    negatives.append(multi[:40] + multi[40 + record:40 + 2 * record] + multi[40:40 + record] + multi[40 + 2 * record:])
    for malformed in negatives:
        try:
            decode(malformed)
        except Exception:
            pass
        else:
            raise AssertionError("malformed model package accepted")
    try:
        decode(vector, b"wrong-synthetic-password")
    except Exception:
        pass
    else:
        raise AssertionError("wrong model password accepted")
    print(f"PASS independent Python model: pinned vector, 6 chunk-boundary round trips, {len(negatives) + 1} negative cases")
    print("NOT executed: C# codec/service/WPF/DPAPI or genuine new-PC/Windows restore. No certification.")


if __name__ == "__main__":
    main()
