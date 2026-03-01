# Niblink — Mistral Worldwide Hackathon 2026

**AI-powered food & symptom tracker for Hashimoto's thyroiditis.**

Built during the [Mistral Worldwide Hackathon](https://worldwide-hackathon.mistral.ai/) (Feb 28 – Mar 1, 2026).

---

## The Problem

People with Hashimoto's disease experience delayed food reactions (24-72 hours). A meal on Monday can cause brain fog on Wednesday. Humans can't track these patterns — but AI can.

Niblink helps users log meals, symptoms, and medications, then uses AI to find the hidden connections.

## What Was Built During the Hackathon

| Feature | Description | Mistral Models |
|---------|-------------|---------------|
| **Mistral AI Provider** | Full AI backend replacing Claude with Mistral for all features | `mistral-large-latest` (text), `pixtral-large-latest` (vision) |
| **Product Label Scanner** | Snap a photo of ingredient list → traffic-light safety rating | `pixtral-large-latest` (OCR + ingredient extraction) |
| **Meal Advisor Chat** | Context-aware nutrition advice with SSE streaming | `mistral-large-latest` (with user's food/symptom data as context) |
| **Voice Input** | Speak your meal instead of typing | `Voxtral` (speech-to-text) |
| **Local AI Privacy Toggle** | Run all text AI on-device via Ollama — zero cloud | `mistral-nemo` + `mistral-small` via Ollama |

### 4 Commits, 48 Hours

```
77d4475 (Feb 28, 15:31) Mistral hackathon: product scanner, voice integration, and AI provider
b8db57a (Feb 28, 17:59) Voxtral STT, live camera, browser TTS fallback, and bug fixes
d0b31df (Mar  1, 12:37) Meal advisor chat with SSE streaming and local AI privacy toggle
e71572a (Mar  1, 13:27) Local vision support via Ollama, JSON mode, and label OCR hardening
```

---

## The Privacy Story

> "All AI features — chat, insights, and image analysis — can run fully on-device. Only voice transcription uses the cloud (with browser STT as offline fallback)."

When the Local AI toggle is enabled:
- **Chat & Insights** → `mistral-nemo` (12B) via Ollama on localhost
- **Food Photo Analysis** → `mistral-small` (24B) via Ollama on localhost
- **Voice** → Browser's built-in Speech Recognition API
- **Zero network requests** for text and insight features

This makes Niblink one of the most privacy-respecting health tracking apps — sensitive health data never needs to leave the user's device.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8, ASP.NET Core, EF Core, SQLite |
| Frontend | Vue 3, TypeScript, Tailwind CSS, Pinia |
| AI (Cloud) | Mistral API (text + vision), Voxtral (STT), ElevenLabs (TTS) |
| AI (Local) | Ollama with mistral-nemo (12B) + mistral-small (24B) |
| Architecture | Clean Architecture (Domain/Application/Infrastructure/Api) |

---

## Key Technical Highlights

### Product Label Scanner
- Dual-prompt system: `LabelExtractionPrompt` (OCR-focused, reads printed text) vs `ImageExtractionPrompt` (visual food identification)
- Rule-based safety engine: checks soy content, goitrogens, iodine risk (algae/seaweed), E-number database, and personal trigger correlations
- Traffic-light UI with per-ingredient analysis and personalized warnings

### Meal Advisor Chat (SSE Streaming)
- Server-Sent Events for real-time token streaming
- System prompt enriched with user's actual data: recent meals, nutrient intake vs. DGE targets, top food-symptom correlations
- Markdown rendering in chat bubbles with "thinking" indicator for local AI latency

### Local AI Resilience
- `response_format: {"type": "json_object"}` enforces structured output from local models
- `FlexibleStringConverter` handles local models returning objects where strings are expected
- Truncated JSON repair for responses cut off by `max_tokens`
- 3x timeout for local vision (image processing is slower on-device)
- Quality warning banner when using local AI for label scanning (OCR limitation)

---

## Project Context

This repo contains **only the code written during the hackathon** (25 new source files). The full Niblink application includes additional features built over prior months:

- BLS v4.0 nutrient database (7,100+ German foods)
- USDA FoodData Central fallback (8,800+ international foods)
- DGE reference values personalized by age, sex, and life stage
- Statistical correlation engine for food-symptom pattern detection
- Supplement tracking with upper-limit warnings
- Full i18n (German + English)
- PWA with offline support

---

## How to Run (Full App)

```bash
# Backend
dotnet run --project src/TriggerDetective.Api  # Port 5005

# Frontend
cd frontend/trigger-detective-web
npm install && npm run dev  # Port 5173, proxies /api to 5005

# Seed test data
curl -X POST http://localhost:5005/api/v1/dev/seed-test-user

# Local AI (optional)
ollama pull mistral-nemo && ollama pull mistral-small
ollama serve  # Port 11434
```

---

## Team

Solo project by [@sorinio](https://github.com/sorinio)

Built with assistance from Claude Code (Anthropic).
