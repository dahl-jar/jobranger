from __future__ import annotations

from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, RootModel, StrictInt, StrictStr


class EnvelopeHello(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    channel: Annotated[
        StrictStr, Field(max_length=43, pattern="^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$")
    ]
    sid: Annotated[StrictStr, Field(max_length=22, pattern="^[A-Za-z0-9_-]{21}[AQgw]$")]
    seq: Annotated[StrictInt, Field(ge=0, le=0)]
    kind: Literal["hello"]
    ciphertext: Annotated[
        StrictStr,
        Field(
            max_length=61440,
            min_length=4,
            pattern="^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$",
        ),
    ]


class EnvelopeData(BaseModel):
    model_config = ConfigDict(
        extra="ignore",
    )
    v: Annotated[StrictInt, Field(ge=1, le=1)]
    channel: Annotated[
        StrictStr, Field(max_length=43, pattern="^[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$")
    ]
    sid: Annotated[StrictStr, Field(max_length=22, pattern="^[A-Za-z0-9_-]{21}[AQgw]$")]
    seq: Annotated[StrictInt, Field(ge=0, le=9007199254740991)]
    kind: Literal["data"]
    ciphertext: Annotated[
        StrictStr,
        Field(
            max_length=61440,
            min_length=4,
            pattern="^(?:[A-Za-z0-9_-]{4})*(?:[A-Za-z0-9_-][AQgw]|[A-Za-z0-9_-]{2}[AEIMQUYcgkosw048])?$",
        ),
    ]


class Envelope(RootModel[EnvelopeHello | EnvelopeData]):
    root: Annotated[
        EnvelopeHello | EnvelopeData, Field(discriminator="kind", title="Envelope")
    ]
