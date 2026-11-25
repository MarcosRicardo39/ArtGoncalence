import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Sessao {
  id: string;
  emailConfirmado: boolean;
}

export interface Perfil {
  id: string;
  nomePublico: string;
  bio: string | null;
  apoio?: { provedor: string; url: string };
}

@Injectable({ providedIn: 'root' })
export class ContaApi {
  private readonly http = inject(HttpClient);
  private readonly estado = signal<Sessao | null>(null);
  readonly sessao = this.estado.asReadonly();
  emailInformado = '';

  async atualizarSessao(): Promise<Sessao | null> {
    try {
      const sessao = await firstValueFrom(this.http.get<Sessao>('/api/contas/sessao'));
      this.estado.set(sessao);
      return sessao;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.estado.set(null);
        return null;
      }
      throw error;
    }
  }

  async autenticar(email: string, senha: string): Promise<void> {
    await this.enviar('login', { email, senha });
    await this.atualizarSessao();
  }

  async sair(): Promise<void> {
    await this.enviar('sair', {});
    this.limparSessao();
  }

  limparSessao(): void {
    this.estado.set(null);
    this.emailInformado = '';
  }

  enviar(operacao: string, dados: object): Promise<void> {
    return this.mutarComCsrf('POST', `/api/contas/${operacao}`, dados);
  }

  perfil(id: string): Promise<Perfil> {
    return firstValueFrom(this.http.get<Perfil>(`/api/artistas/${encodeURIComponent(id)}`));
  }

  editarPerfil(nomePublico: string, bio: string | null): Promise<void> {
    return this.mutarComCsrf('PUT', '/api/artistas/me', { nomePublico, bio });
  }

  definirApoio(url: string | null): Promise<void> {
    return this.mutarComCsrf('PUT', '/api/artistas/me/apoio', { url });
  }

  async mutarComCsrf<T = void>(metodo: 'POST' | 'PUT', url: string, body: object): Promise<T> {
    await firstValueFrom(this.http.get<void>('/api/contas/csrf'));
    return firstValueFrom(this.http.request<T>(metodo, url, { body }));
  }
}

export function mensagemDeErro(error: unknown, padrao: string): string {
  if (!(error instanceof HttpErrorResponse)) return padrao;

  switch (error.status) {
    case 0:
      return 'Não foi possível conectar ao ArtGoncalense. Tente novamente.';
    case 429:
      return 'Você fez muitas tentativas. Aguarde alguns minutos e tente novamente.';
    case 503:
      return 'Este serviço está temporariamente indisponível. Tente novamente mais tarde.';
    default:
      return padrao;
  }
}
