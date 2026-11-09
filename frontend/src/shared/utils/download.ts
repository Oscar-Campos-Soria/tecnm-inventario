import { http } from '../api/http'

/**
 * Descarga un archivo que genera la API (PDF, Excel) con el nombre que manda
 * el servidor. Con <a download> no lo frena el bloqueador de ventanas.
 */
export async function downloadFile(path: string): Promise<void> {
  const { blob, fileName } = await http.download(path)
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  // El navegador ya tomó el blob; se libera después para no cortar la descarga.
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}
