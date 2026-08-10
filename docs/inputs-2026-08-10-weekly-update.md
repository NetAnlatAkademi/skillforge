# SkillForge — Haftalık Ekosistem Güncellemesi

**Tarih:** 2026-08-10
**Kapsam:** Agent Skills, MCP, registry, güvenlik, sağlayıcı duyuruları ve SkillForge ürün etkileri

> **Arşiv notu.** Bu, `SKILLFORGE_ROADMAP.md §32` ve `TODO.md § v0.6` bölümlerinin kaynağı olan harici girdinin
> arşiv kopyasıdır. İçindeki dış dünya iddiaları **ikinci eldendir ve bu repoda doğrulanmamıştır.** Yön tayini için
> kullanılabilirler; bir yatırımı veya bir kural şiddetini gerekçelendirmek için önce birincil kaynaklara bakılmalıdır.
> Bu dosya olduğu gibi saklanır: v0.6 gerçeklenirken alınan kararlar bundan bazı noktalarda ayrılır, ve ayrılma
> gerekçeleri `CHANGELOG.md` ile `docs/validation-rules.md` içinde kayıtlıdır.

---

## 1. Bu Haftanın En Önemli Gelişmesi

GitHub, **6 Ağustos 2026** tarihinde Enterprise müşterileri için Copilot istemcilerinde çalıştırılabilecek MCP
sunucularını merkezi olarak yönetmeye yönelik allow/deny politikalarını genel kullanıma açtı.

Desteklenen politika yaklaşımı:

- Remote MCP server URL
- Local command
- Server name
- Allow list
- Deny list
- Canonicalized URL matching
- Policy doğrulanamadığında fail-closed davranış

Politika şu ortamlarda uygulanıyor:

- GitHub Copilot app
- GitHub Copilot CLI
- VS Code

---

## 2. SkillForge Açısından Anlamı

Bu gelişme, kurumsal pazarda ana ihtiyacın yalnızca MCP server keşfetmek veya kurmak olmadığını daha net gösteriyor.

Asıl ihtiyaç:

> Hangi MCP sunucusunun, hangi agent tarafından, hangi koşullarda kullanılabileceğini merkezi olarak tanımlamak,
> doğrulamak ve değişikliklerini izlemek.

SkillForge'un `policy-as-code` yaklaşımı bu nedenle daha stratejik hale geliyor.

---

## 3. Policy Modeli İçin Öneri

SkillForge policy dosyasında MCP kuralları açık biçimde tanımlanmalıdır.

Örnek:

```yaml
schemaVersion: 1

mcp:
  default: deny

  allow:
    - serverUrl: "https://mcp.company.com/*"

    - serverCommand:
        command: "npx"
        args:
          - "@company/internal-mcp"

  deny:
    - serverUrl: "http://*"
```

### Tasarım İlkeleri

- Varsayılan davranış `deny` olmalı.
- Belirsiz veya parse edilemeyen policy durumunda fail-closed davranılmalı.
- URL ve local command ayrı kurallarla ele alınmalı.
- Display name güvenlik açısından tek başına yeterli kimlik olarak kullanılmamalı.
- Wildcard genişlemeleri risk olarak gösterilmeli.
- Policy değişiklikleri diff edilebilmeli.

---

## 4. Yeni Önerilen Komut

Roadmap'e aşağıdaki komut eklenmelidir:

```bash
skillforge policy diff origin/main...HEAD
```

Amaç:

- Policy genişlemelerini tespit etmek
- Allow list değişikliklerini göstermek
- Deny kurallarının kaldırılmasını tespit etmek
- Wildcard kullanımındaki riskli genişlemeleri göstermek
- Yeni MCP server izinlerini görünür hale getirmek

Örnek çıktı:

```text
MCP Policy Diff

+ Allowed:
  https://mcp.company.com/github

- Removed:
  local command: npx unknown-server

! Policy weakened:
  wildcard changed
  api.company.com
  →
  *.company.com
```

---

## 5. Roadmap Güncellemesi

Önceki plan:

```text
v0.1
Local validator + inspect + SARIF

v0.2
skillforge diff + GitHub Action

v0.3
policy-as-code

v0.4
MCP compatibility
```

Güncellenmiş öneri:

```text
v0.1
├── init
├── validate
├── inspect
├── pack
└── SARIF

v0.2
├── skillforge diff
├── GitHub Action
├── permission diff
├── external URL diff
├── package hash diff
└── PR annotations

v0.3
├── policy-as-code
├── MCP allow/deny rules
├── policy diff
├── permission drift
├── fail-closed validation
├── rule suppression
└── provenance policy

v0.4
├── MCP protocol compatibility
├── MCP version adapters
├── OAuth/OIDC validation
├── JSON Schema validation
├── extension validation
└── deprecated capability detection
```

---

## 6. v0.3 İçin Yeni Öncelikler

### 6.1 `skillforge policy check`

```bash
skillforge policy check \
  --policy .skillforge/policy.yaml
```

Kontroller:

- MCP allow/deny
- Shell izni
- Filesystem read/write
- Network domain allowlist
- Provenance
- Package hash
- Skill metadata
- Agent compatibility

### 6.2 `skillforge policy diff`

```bash
skillforge policy diff origin/main...HEAD
```

Kontroller:

- Allow list genişledi mi?
- Deny list daraldı mı?
- Wildcard genişledi mi?
- Yeni external domain geldi mi?
- Yeni local executable izni geldi mi?
- Fail-closed davranışı zayıflatıldı mı?

### 6.3 MCP Policy Diagnostic Kodları

| Kod | Seviye | Açıklama |
|---|---|---|
| SF8101 | Error | MCP server policy tarafından engellendi |
| SF8102 | Warning | MCP wildcard kapsamı genişledi |
| SF8103 | Error | Policy parse edilemedi |
| SF8104 | Error | Policy fail-open davranıyor |
| SF8105 | Warning | MCP server display name ile eşleşiyor |
| SF8106 | Warning | Yeni local MCP command eklendi |
| SF8107 | Warning | Yeni remote MCP domain eklendi |

---

## 7. Uzun Vadeli Ürün Yönü

Bu haftaki gelişme SkillForge'un daha geniş bir role evrilebileceğini gösteriyor.

Uzun vadeli ürün tanımı:

> Vendor-bağımsız Agent Configuration Policy Engine

Kapsanabilecek kaynaklar:

```text
GitHub Copilot policy
Codex configuration
Claude configuration
MCP configuration
Agent Skills
AGENTS.md
CLAUDE.md
hooks
tool permissions
```

Böylece SkillForge yalnızca skill validator değil, kurum genelinde agent davranışı ve yapılandırmasını denetleyen bir
policy katmanına dönüşebilir.

---

## 8. Rekabet Açısından Çıkarım

Sağlayıcılar kendi ekosistemlerinde:

- Skill kurulumunu
- MCP bağlantılarını
- Agent konfigürasyonunu
- Basit allow/deny politikalarını

giderek daha fazla kendi ürünlerine ekliyor.

Bu nedenle SkillForge şu alanlarda farklılaşmalıdır:

1. Vendor bağımsızlık
2. CI/CD entegrasyonu
3. Policy diff
4. Permission drift
5. Provenance
6. Version-to-version behavior diff
7. SARIF
8. Cross-provider compatibility
9. Organization-wide inventory
10. Evidence-based security findings

---

## 9. Bu Haftanın Net Ürün Kararı

Ana roadmap değişmeyecek.

Ancak:

> MCP policy kontrolü v0.4'ten v0.3'e çekilecek.

Yeni kritik özellik:

```bash
skillforge policy diff origin/main...HEAD
```

Bu özellik, agent ve MCP policy değişikliklerinin kod değişiklikleri kadar görünür ve denetlenebilir hale gelmesini
sağlayacak.

---

## 10. Güncellenmiş Ürün Tezi

SkillForge'un ilk ticari değer yaratabilecek konumu:

> Agent Skills, MCP ve agent configuration değişikliklerini CI aşamasında doğrulayan, riskli permission ve policy
> drift'i gösteren vendor-bağımsız policy ve provenance platformu.

İlk üç kritik ürün özelliği:

```text
skillforge diff
skillforge policy check
skillforge policy diff
```

Marketplace, public registry ve web dashboard bu üç alan gerçek ekiplerde doğrulanmadan geliştirilmemelidir.

---

## Gerçeklenirken Bu Belgeden Ayrılan Noktalar

Aşağıdakiler v0.6'da bilinçli olarak farklı yapıldı. Gerekçeler `CHANGELOG.md [26.222.1]` ve
`docs/validation-rules.md` içinde ayrıntılı:

| Belgedeki öneri | Gerçeklenen | Neden |
|---|---|---|
| `mcp:` kök seviyede | `rules.mcp:` (ve kök seviye de kabul ediliyor) | Dosyanın geri kalanıyla tutarlılık |
| Beyan edilmemiş `default` için fail-closed = deny uygula | Hiçbir varsayılan uygulanmaz; `SF8104` ile run düşer | Deny varsaymak, her sunucu için uydurma bir `SF8101` üretip asıl sorunu — kararın yazılmamış olmasını — gömerdi |
| SF8103 "Policy parse edilemedi" | Yalnızca `mcp` bölümündeki yorumlanamayan girdi | Dosyanın tümü parse edilemediğinde zaten `SF9001` var; yayınlanmış bir kodun anlamı değişmez |
| `policy diff origin/main...HEAD` | İki yol alır | `skillforge diff` ile aynı kısıt; `docs/ci.md`'deki `git worktree` tarifi aynı işi görür |
| — | `SF9010` eklendi | `mcp` dışındaki gevşemelerin de bir kodu olması gerekiyordu |
