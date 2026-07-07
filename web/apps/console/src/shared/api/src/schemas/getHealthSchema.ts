import { z } from "zod";

 /**
 * @description OK
 */
export const getHealth200Schema = z.any();

 export const getHealthQueryResponseSchema = z.lazy(() => getHealth200Schema);