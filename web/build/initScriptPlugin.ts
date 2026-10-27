import { build, type Plugin, type Rollup } from 'vite'

const SCRIPT_EXTENSION = '.js'
const IIFE_NAME = 'jobrangerInitScript'

export async function bundleInitScript(entry: string, fileName: string): Promise<string> {
  const result = await build({
    configFile: false,
    publicDir: false,
    logLevel: 'silent',
    build: {
      write: false,
      minify: true,
      lib: { entry, formats: ['iife'], name: IIFE_NAME },
    },
  })
  const [bundle] = [result].flat()
  if (bundle === undefined || !('output' in bundle)) {
    throw new Error(`${fileName.replace(SCRIPT_EXTENSION, '')} build of ${entry} returned no bundle`)
  }
  return bundle.output[0].code
}

export function initScriptPlugin(entry: string, fileName: string) {
  return {
    name: `jobranger-${fileName.replace(SCRIPT_EXTENSION, '')}`,
    configureServer(server) {
      server.middlewares.use(`/${fileName}`, (_request, response, next) => {
        bundleInitScript(entry, fileName).then((script) => {
          response.setHeader('Content-Type', 'text/javascript')
          response.end(script)
        }, next)
      })
    },
    async generateBundle(this: Pick<Rollup.PluginContext, 'emitFile'>) {
      this.emitFile({ type: 'asset', fileName, source: await bundleInitScript(entry, fileName) })
    },
  } satisfies Plugin
}

