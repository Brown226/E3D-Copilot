/**
 * highlight.js 按需语言注册
 * 只导入 E3D 场景常用语言，避免全量引入 (~400KB → ~15-20KB)
 */

import json from 'highlight.js/lib/languages/json'
import python from 'highlight.js/lib/languages/python'
import bash from 'highlight.js/lib/languages/bash'
import sql from 'highlight.js/lib/languages/sql'
import xml from 'highlight.js/lib/languages/xml'
import yaml from 'highlight.js/lib/languages/yaml'

export const languages = {
  json,
  python,
  bash,
  sql,
  xml,
  yaml,
}
