INSERT INTO roles (Id, Name, PermissionsJson) VALUES
(NEWID(), 'Admin',   '{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u","d"],"audit":["r"],"users":["r","c","u","d"]}'),
(NEWID(), 'Manager', '{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u"],"audit":[],"users":["r"]}'),
(NEWID(), 'Auditor', '{"notes":["r"],"tasks":["r"],"contacts":[],"audit":["r"],"users":["r"]}'),
(NEWID(), 'User',    '{"notes":["r","c","u","d"],"tasks":["r","c","u","d"],"contacts":["r","c","u","d"],"audit":[],"users":[]}');
SELECT Name, Id FROM roles;
