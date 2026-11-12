<script setup lang="ts">
import { computed } from 'vue'

// Pinta el texto del modelo con su Markdown sencillo (párrafos, listas, **negritas**) SIN v-html:
// las respuestas citan títulos de tickets que escribieron usuarios, y eso nunca debe ejecutarse como HTML.
const props = defineProps<{ text: string }>()

type Inline = { text: string; bold: boolean }
type Block = { kind: 'p' | 'ul' | 'ol'; lines: Inline[][] }

const LIST = /^\s*([-*•]|\d+[.)])\s+/

function inline(line: string): Inline[] {
  return line
    .replace(/^#{1,6}\s+/, '')
    .split(/\*\*(.+?)\*\*/g)
    .map((part, i) => ({ text: part, bold: i % 2 === 1 }))
    .filter((part) => part.text.length > 0)
}

const blocks = computed<Block[]>(() => {
  const result: Block[] = []
  for (const raw of props.text.split('\n')) {
    const line = raw.trimEnd()
    if (line.trim() === '') {
      result.push({ kind: 'p', lines: [] })
      continue
    }
    const marker = LIST.exec(line)
    const kind: Block['kind'] = marker ? (/\d/.test(marker[1]) ? 'ol' : 'ul') : 'p'
    const content = inline(marker ? line.slice(marker[0].length) : line)
    const last = result.at(-1)
    if (last && last.kind === kind && (kind !== 'p' || last.lines.length > 0)) last.lines.push(content)
    else result.push({ kind, lines: [content] })
  }
  return result.filter((b) => b.lines.length > 0)
})
</script>

<template>
  <div class="space-y-2">
    <template v-for="(block, b) in blocks" :key="b">
      <p v-if="block.kind === 'p'">
        <template v-for="(line, l) in block.lines" :key="l">
          <br v-if="l > 0" />
          <template v-for="(part, i) in line" :key="i">
            <strong v-if="part.bold" class="font-semibold">{{ part.text }}</strong>
            <template v-else>{{ part.text }}</template>
          </template>
        </template>
      </p>
      <component :is="block.kind" v-else :class="['space-y-1 pl-5', block.kind === 'ol' ? 'list-decimal' : 'list-disc']">
        <li v-for="(line, l) in block.lines" :key="l">
          <template v-for="(part, i) in line" :key="i">
            <strong v-if="part.bold" class="font-semibold">{{ part.text }}</strong>
            <template v-else>{{ part.text }}</template>
          </template>
        </li>
      </component>
    </template>
  </div>
</template>
