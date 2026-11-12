-- ============================================================
-- OPCIONAL, SOLO DESARROLLO: fabricantes y modelos de ejemplo para probar el
-- asistente IA y la carga masiva (los activos ZZ-IA-* se suben por la API para
-- que queden en audit_log). Requiere seed-locations-demo.sql para las ubicaciones.
-- Idempotente: seguro de re-ejecutar (ON CONFLICT DO NOTHING).
-- Uso: psql ... -v ON_ERROR_STOP=1 -f scripts/seed-assistant-demo.sql
-- ============================================================

SET search_path TO inventario, public;

INSERT INTO manufacturers (name) VALUES
  ('HP'), ('Dell'), ('Lenovo'), ('Epson'), ('Cisco'), ('BenQ'), ('APC')
ON CONFLICT (name) DO NOTHING;

INSERT INTO models (manufacturer_id, asset_type_id, model_name)
SELECT m.manufacturer_id, t.asset_type_id, v.model_name
FROM (VALUES
  ('HP', 'LAPTOP', 'ProBook 450 G8'),
  ('HP', 'PC', 'ProDesk 400 G7'),
  ('HP', 'PRINTER', 'LaserJet M404'),
  ('Dell', 'PC', 'OptiPlex 7090'),
  ('Dell', 'LAPTOP', 'Latitude 3420'),
  ('Lenovo', 'LAPTOP', 'ThinkPad E14'),
  ('Lenovo', 'MONITOR', 'ThinkVision T24i'),
  ('Epson', 'PRINTER', 'L3250'),
  ('Cisco', 'SWITCH', 'Catalyst 2960'),
  ('Cisco', 'SWITCH', 'Catalyst 9200'),
  ('BenQ', 'PROJECTOR', 'MS560')
) AS v(manufacturer, asset_type, model_name)
JOIN manufacturers m ON m.name = v.manufacturer
JOIN asset_types t ON t.code = v.asset_type
ON CONFLICT (manufacturer_id, model_name) DO NOTHING;
