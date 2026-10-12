import { build, type Plugin, type Rollup } from 'vite'

const THEME_INIT_FILE = 'theme-init.js'

const IIFE_NAME = 'jobrangerThemeInit'

export async function bundleThemeInit(entry: string): Promise<string> {
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
  if (bundle === undefined || !('output' in bundle)) throw new Error(`theme-init build of ${entry} returned no bundle`)
  return bundle.output[0].code
}

export function themeInitPlugin(entry: string) {
  return {
    name: 'jobranger-theme-init',
    configureServer(server) {
      server.middlewares.use(`/${THEME_INIT_FILE}`, (_request, response, next) => {
        bundleThemeInit(entry).then((script) => {
          response.setHeader('Content-Type', 'text/javascript')
          response.end(script)
        }, next)
      })
    },
    async generateBundle(this: Pick<Rollup.PluginContext, 'emitFile'>) {
      this.emitFile({ type: 'asset', fileName: THEME_INIT_FILE, source: await bundleThemeInit(entry) })
    },
  } satisfies Plugin
}
