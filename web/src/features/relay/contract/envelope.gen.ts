export type Envelope = EnvelopeHello | EnvelopeData;
export type Channel = string;
export type Sid = string;
export type Base64Url = string;
export type Seq = number;

export interface EnvelopeHello {
  v: 1;
  channel: Channel;
  sid: Sid;
  seq: 0;
  kind: "hello";
  ciphertext: Base64Url;
}
export interface EnvelopeData {
  v: 1;
  channel: Channel;
  sid: Sid;
  seq: Seq;
  kind: "data";
  ciphertext: Base64Url;
}
