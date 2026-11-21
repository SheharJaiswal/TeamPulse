import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = ({ cookies }) => {
  redirect(303, cookies.get('teampulse_session') ? '/habits' : '/login');
};
