create table user_master (
id SERIAL PRIMARY KEY,
name VARCHAR(100) NOT NULL,
email VARCHAR(100) UNIQUE NOT NULL,
age INT
);

INSERT into user_master (name,email,age) values ('mario','abc@gsail.com',28);
INSERT into user_master (name,email,age) values ('neo','neo@gsail.com',32);