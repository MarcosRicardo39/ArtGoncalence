import { Routes } from '@angular/router';
import { Acesso } from './acesso';
import { PerfilArtista } from './perfil';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'conta/entrar' },
  {
    path: 'conta/entrar',
    component: Acesso,
    data: { modo: 'entrar' },
    title: 'Entrar | ArtGoncalense',
  },
  {
    path: 'conta/cadastro',
    component: Acesso,
    data: { modo: 'cadastro' },
    title: 'Cadastro | ArtGoncalense',
  },
  {
    path: 'conta/confirmar-email',
    component: Acesso,
    data: { modo: 'confirmar' },
    title: 'Confirmar e-mail | ArtGoncalense',
  },
  {
    path: 'conta/reenviar-confirmacao',
    component: Acesso,
    data: { modo: 'reenviar' },
    title: 'Reenviar confirmação | ArtGoncalense',
  },
  {
    path: 'conta/recuperar-senha',
    component: Acesso,
    data: { modo: 'recuperar' },
    title: 'Recuperar senha | ArtGoncalense',
  },
  {
    path: 'conta/redefinir-senha',
    component: Acesso,
    data: { modo: 'redefinir' },
    title: 'Redefinir senha | ArtGoncalense',
  },
  {
    path: 'conta/perfil',
    component: PerfilArtista,
    data: { editavel: true },
    title: 'Meu perfil | ArtGoncalense',
  },
  { path: 'artistas/:id', component: PerfilArtista, title: 'Perfil de artista | ArtGoncalense' },
  { path: '**', redirectTo: 'conta/entrar' },
];
