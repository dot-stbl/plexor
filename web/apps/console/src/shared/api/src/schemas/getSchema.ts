import { z } from "zod";

 /**
 * @description OK
 */
export const get200Schema = z.any();

 export const getQueryResponseSchema = z.lazy(() => get200Schema);