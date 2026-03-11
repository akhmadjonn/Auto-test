# Avtolider — Driving License Exam Preparation Platform (Uzbekistan)

## Project Overview
Avtolider is a subscription-based web platform for the Uzbekistan UBDD driving license theory exam. Users practice 1,200+ questions (text + images, Uzbek Latin + Cyrillic + Russian), take timed mock exams (20 questions, 25 min, pass=18/20), practice by ticket/category/marathon, track progress with analytics, and subscribe via Payme/Click.

## Tech Stack
- Backend: .NET 8, ASP.NET Core Web API, Clean Architecture, CQRS (MediatR), EF Core 8 + PostgreSQL 16, Redis 7, FluentValidation, ClosedXML, SixLabors.ImageSharp
- Frontend: Next.js 15 (App Router), TypeScript, Tailwind CSS v4, shadcn/ui, Zustand, Recharts, React Hook Form + Zod
- Infrastructure: Docker Swarm, Traefik v3, MinIO (S3-compatible image storage), Nginx
- Payments: Payme (Subscribe API for card tokenization + recurring), Click (Card Token API)
- Auth: SMS OTP via Eskiz.uz, Telegram Login Widget, JWT (access + refresh tokens)
- Languages: Uzbek Cyrillic (uz), Uzbek Latin (uzLatin), Russian (ru) — all content is TRILINGUAL

## Architecture Rules (ALL AGENTS MUST FOLLOW)
1. API contract is the single source of truth. Both agents code against contracts/openapi.yaml. Never define API types manually.
2. Clean Architecture dependency rule: Domain → zero deps. Application → Domain only. Infrastructure → Application interfaces. Api → wires everything.
3. CQRS via MediatR: Every API action is a Command (write) or Query (read). No business logic in controllers.
4. All user-facing content is TRILINGUAL. Store as JSONB {"uz": "Cyrillic", "uzLatin": "Latin", "ru": "Russian"}. Frontend renders based on user's locale.
5. Amounts in tiyins. 1 UZS = 100 tiyins. All money values are long (C#) / number (TS), never decimal/float.
6. UTC everywhere. Store DateTimeOffset in UTC. Frontend converts to Asia/Tashkent (UTC+5).
7. Snake_case in PostgreSQL, PascalCase in C#, camelCase in JSON API responses.
8. No business logic in controllers. Controllers: parse request → send MediatR command/query → return response.
9. Every endpoint returns ApiResponse<T> with success, data, error fields.
10. Questions and answer options can BOTH have images independently. Four scenarios: text-only, question-image + text-answers, text-question + image-answers, both have images.

## Uzbekistan Driving Exam Facts
- 20 questions per exam, 25 minutes, pass = 18/20 correct
- ~1,200 questions total, organized into tickets (biletlar) of 20
- 28 categories matching official YHQ (Yo'l Harakati Qoidalari) chapters
- Questions include images (road signs, traffic diagrams)
- AB and CD license categories
- Languages: Uzbek (Latin + Cyrillic), Russian

## Exam Modes
- **Exam (Imtihon)**: random 20 questions, 25 min timer, matching real UBDD exam
- **Ticket (Bilet)**: select ticket 1-57+, get that ticket's fixed 20 questions, 25 min timer
- **Marathon (Maraton)**: ALL 1,200 questions, NO timer, progress saves, can resume

## Git Workflow
- main — production, develop — integration
- feature/backend-*, feature/frontend-*
- Agents MUST NOT modify files outside their directory (backend/ or frontend/) except contracts/
