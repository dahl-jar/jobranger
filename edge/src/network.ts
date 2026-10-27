export interface ClientKeys {
  readonly client: string
  readonly network: string
}

const UNKNOWN: ClientKeys = { client: 'unknown', network: 'unknown' }

const IPV4_OCTETS = 4
const IPV4_MAX_OCTET = 255
const IPV4_NETWORK_OCTETS = 3
const IPV4_NETWORK_BITS = 24
const IPV6_GROUPS = 8
const IPV6_MAX_GROUP = 0xffff
const IPV6_CLIENT_GROUPS = 4
const IPV6_CLIENT_BITS = 64
const IPV6_NETWORK_GROUPS = 3
const IPV6_NETWORK_BITS = 48
const IPV4_MAPPED_PREFIX = [0, 0, 0, 0, 0, IPV6_MAX_GROUP]
const BITS_PER_OCTET = 8
const OCTET_MASK = 0xff
const HEX_RADIX = 16
const DECIMAL_OCTET = /^(0|[1-9]\d{0,2})$/
const IPV6_CHARACTERS = /^[0-9a-f:.]+$/i
const COMPRESSION = '::'

export function clientKeys(address: string | null): ClientKeys {
  if (address === null) {
    return UNKNOWN
  }

  const ipv4 = parseIpv4(address)
  if (ipv4 !== undefined) {
    return ipv4Keys(ipv4)
  }

  const ipv6 = parseIpv6(address)
  if (ipv6 === undefined) {
    return UNKNOWN
  }

  const mapped = mappedIpv4(ipv6)
  return mapped === undefined ? ipv6Keys(ipv6) : ipv4Keys(mapped)
}

function ipv4Keys(octets: readonly number[]): ClientKeys {
  return {
    client: octets.join('.'),
    network: `${[...octets.slice(0, IPV4_NETWORK_OCTETS), 0].join('.')}/${IPV4_NETWORK_BITS}`,
  }
}

function ipv6Keys(groups: readonly number[]): ClientKeys {
  return {
    client: `${hexGroups(groups, IPV6_CLIENT_GROUPS)}::/${IPV6_CLIENT_BITS}`,
    network: `${hexGroups(groups, IPV6_NETWORK_GROUPS)}::/${IPV6_NETWORK_BITS}`,
  }
}

function hexGroups(groups: readonly number[], count: number): string {
  return groups
    .slice(0, count)
    .map((group) => group.toString(HEX_RADIX))
    .join(':')
}

function parseIpv4(address: string): number[] | undefined {
  const parts = address.split('.')
  if (parts.length !== IPV4_OCTETS || !parts.every((part) => DECIMAL_OCTET.test(part))) {
    return undefined
  }

  const octets = parts.map(Number)
  return octets.every((octet) => octet <= IPV4_MAX_OCTET) ? octets : undefined
}

function parseIpv6(address: string): number[] | undefined {
  if (!IPV6_CHARACTERS.test(address)) {
    return undefined
  }

  const url = URL.parse(`http://[${address}]/`)
  return url === null ? undefined : expandIpv6(url.hostname.slice(1, -1))
}

function expandIpv6(canonical: string): number[] {
  const [head = '', tail = ''] = canonical.split(COMPRESSION)
  const headGroups = parseGroups(head)
  const tailGroups = parseGroups(tail)
  const zeros = new Array<number>(IPV6_GROUPS - headGroups.length - tailGroups.length).fill(0)
  return [...headGroups, ...zeros, ...tailGroups]
}

function parseGroups(text: string): number[] {
  return text === '' ? [] : text.split(':').map((group) => Number.parseInt(group, HEX_RADIX))
}

function mappedIpv4(groups: readonly number[]): number[] | undefined {
  if (!IPV4_MAPPED_PREFIX.every((group, index) => groups[index] === group)) {
    return undefined
  }

  return groups
    .slice(IPV4_MAPPED_PREFIX.length)
    .flatMap((group) => [group >> BITS_PER_OCTET, group & OCTET_MASK])
}
