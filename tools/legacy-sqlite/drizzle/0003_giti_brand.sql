UPDATE settings
SET value = json_set(
  value,
  '$.brand', 'چاپ گیتی',
  '$.seoTitle', CASE
    WHEN COALESCE(json_extract(value, '$.seoTitle'), '') = ''
      THEN 'چاپ کتاب و خدمات انتشارات | چاپ گیتی'
    ELSE replace(json_extract(value, '$.seoTitle'), 'نقش و نشر', 'چاپ گیتی')
  END
)
WHERE key = 'site';
