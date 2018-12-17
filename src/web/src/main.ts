import 'bulma/bulma.sass';
import 'bulmaswatch/darkly/bulmaswatch.scss';
import 'styles.scss';

// @ts-ignore
import * as Bluebird from 'bluebird';
Bluebird.config({ warnings: { wForgottenReturn: false } });
const fetchPolyfill = !self.fetch
  ? import('isomorphic-fetch' /* webpackChunkName: 'fetch' */)
  : Promise.resolve(self.fetch);

import { Aurelia } from 'aurelia-framework';
import { PLATFORM } from 'aurelia-pal';

export async function configure(aurelia: Aurelia) {
  aurelia.use
    .defaultBindingLanguage()
    .defaultResources()
    .developmentLogging();

  await fetchPolyfill;
  await aurelia.start();
  await aurelia.setRoot(PLATFORM.moduleName('root'));
}
