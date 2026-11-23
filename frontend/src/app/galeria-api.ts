import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ContaApi } from './conta-api';

export interface IlustracaoPublica {
  id: string;
  titulo: string;
  descricao: string;
  categoria: 'ORIGINAL' | 'FANART';
  criadoEm: string;
  artista: { id: string; nomePublico: string };
  imagemUrl: string;
}

export interface GaleriaPublica {
  page: number;
  pageSize: number;
  total: number;
  itens: IlustracaoPublica[];
}

@Injectable({ providedIn: 'root' })
export class GaleriaApi {
  private readonly http = inject(HttpClient);
  private readonly conta = inject(ContaApi);

  listar(page: number, pageSize: number): Promise<GaleriaPublica> {
    return firstValueFrom(
      this.http.get<GaleriaPublica>('/api/ilustracoes', { params: { page, pageSize } }),
    );
  }

  buscar(id: string): Promise<IlustracaoPublica> {
    return firstValueFrom(
      this.http.get<IlustracaoPublica>(`/api/ilustracoes/${encodeURIComponent(id)}`),
    );
  }

  publicar(formulario: FormData): Promise<{ id: string; status: 'PENDENTE' }> {
    return this.conta.mutarComCsrf('POST', '/api/ilustracoes', formulario);
  }
}
