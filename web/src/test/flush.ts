const SETTLE_MS = 300
const DRAIN_TURNS = 20

function nextTurn(): Promise<void> {
  return new Promise((resolve) => {
    const channel = new MessageChannel()
    channel.port1.onmessage = () => {
      channel.port1.close()
      resolve()
    }
    channel.port2.postMessage(undefined)
  })
}

export async function drainTurns(): Promise<void> {
  for (let turn = 0; turn < DRAIN_TURNS; turn += 1) await nextTurn()
}

export async function flush(): Promise<void> {
  const end = performance.now() + SETTLE_MS
  while (performance.now() < end) await nextTurn()
}

const UNTIL_LIMIT_MS = 5000

export async function until(condition: () => boolean): Promise<void> {
  const deadline = performance.now() + UNTIL_LIMIT_MS
  while (!condition()) {
    if (performance.now() > deadline) throw new Error(`condition not met within ${UNTIL_LIMIT_MS} ms`)
    await nextTurn()
  }
}
